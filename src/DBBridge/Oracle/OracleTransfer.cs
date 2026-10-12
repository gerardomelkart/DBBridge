using System.Diagnostics;
using System.Globalization;
using DBBridge.Infrastructure;
using Oracle.ManagedDataAccess.Client;

namespace DBBridge.Oracle;

internal static class OracleTransfer
{
    public static void Execute(OracleTransferDefinition definition, bool daily, string period, RunLog log, CancellationToken cancellation)
    {
        var state = new ExecutionState();
        try
        {
            ExecuteCore(definition, daily, period, log, cancellation, state);
        }
        catch (Exception error)
        {
            string detail = OracleErrors.Describe(error, cancellation);
            log.Write($"FALLO EN {state.Stage}: {detail}");
            log.Write(state.LoadValidated
                ? "CARGA VALIDADA: los datos de la tabla están completos; falló la limpieza de tablas diarias. Consulta el detalle anterior."
                : state.DestinationTouched
                ? "La recreación del destino comenzó; la tabla puede estar vacía o incompleta. Reejecutar."
                : "DESTINO SIN MODIFICAR: no comenzó la recreación ni la inserción.");
            throw;
        }
    }

    private sealed class ExecutionState
    {
        public string Stage { get; set; } = "Preparando conexiones";
        public bool DestinationTouched { get; set; }
        public bool LoadValidated { get; set; }
    }

    private static void ExecuteCore(OracleTransferDefinition definition, bool daily, string period, RunLog log, CancellationToken cancellation, ExecutionState state)
    {
        string tableName = daily ? OracleDailyTables.TableName(definition.TablePrefix, period) : $"{definition.TablePrefix}{period}";
        string table = $"{definition.Schema}.{tableName}";
        var connections = definition.Connect();
        using var origin = connections.Origin;
        using var destination = connections.Destination;
        var totalTimer = Stopwatch.StartNew();
        using var heartbeat = new RunHeartbeat(log);
        void Phase(string value)
        {
            state.Stage = value;
            heartbeat.SetPhase(value);
            log.Write(value);
        }
        log.Write($"EQUIPO={Environment.MachineName}; proceso={Environment.ProcessId}; motor Oracle sin cambios; diagnóstico por etapas.");
        log.Write($"Conectando a origen {definition.OriginAddress} y destino {definition.DestinationAddress}.");
        state.Stage = $"Conexión ORIGEN {definition.OriginAddress}";
        log.Write(state.Stage);
        origin.Open();
        log.Write("Conexión ORIGEN correcta.");
        state.Stage = $"Conexión DESTINO {definition.DestinationAddress}";
        log.Write(state.Stage);
        destination.Open();
        log.Write("Conexión DESTINO correcta.");
        log.Write($"Oracle origen={origin.ServerVersion}; destino={destination.ServerVersion}; tabla={table}");
        using var select = origin.CreateCommand();
        definition.PrepareQuery(select, period, daily, log);
        select.CommandTimeout = 0; // La consulta de millones puede tardar; Ctrl+C cancela.
        select.FetchSize = 32 * 1024 * 1024;
        using var registration = cancellation.Register(() =>
        {
            try { select.Cancel(); } catch { /* Cancelación de mejor esfuerzo. */ }
        });
        cancellation.ThrowIfCancellationRequested();
        Phase($"ORIGEN {definition.Name}: ejecutando consulta (ExecuteReader); destino sin modificar");
        log.Write(definition.QueryMessage);
        var firstTimer = Stopwatch.StartNew();
        using var reader = select.ExecuteReader();
        log.Write($"ExecuteReader completado={firstTimer.Elapsed.TotalSeconds:F1}s; todavía pendiente de la primera lectura.");
        Phase($"ORIGEN {definition.Name}: leyendo estructura de columnas; destino sin modificar");
        var metadataTimer = Stopwatch.StartNew();
        var columns = ColumnDefinition.Read(reader);
        if (definition.ExpectedColumns is int expected && columns.Length != expected)
        {
            throw new InvalidOperationException($"{definition.Name} requiere {expected} columnas; origen devolvió {columns.Length}; destino sin modificar.");
        }
        log.Write($"ESTRUCTURA ORIGEN: columnas={columns.Length}; lectura de metadatos={metadataTimer.Elapsed.TotalSeconds:F1}s.");
        reader.FetchSize = Math.Max(reader.RowSize, 32L * 1024 * 1024);
        log.Write($"LECTOR ORIGEN: tamaño de fila={reader.RowSize:N0} bytes; fetch={reader.FetchSize:N0} bytes.");
        Phase($"ORIGEN {definition.Name}: solicitando primera fila (Read); destino sin modificar");
        var fetchTimer = Stopwatch.StartNew();
        bool hasRow = reader.Read();
        log.Write($"PRIMER READ COMPLETADO: tiempo={fetchTimer.Elapsed.TotalSeconds:F1}s; filas disponibles={hasRow}.");
        firstTimer.Stop();
        cancellation.ThrowIfCancellationRequested();
        log.Write($"Primera lectura={firstTimer.Elapsed.TotalSeconds:F1}s; columnas={columns.Length}; filas disponibles={hasRow}");
        // Metadatos y primera lectura correctos antes del DROP.
        heartbeat.SetPhase("Recreando tabla destino");
        state.Stage = "Recreación de tabla DESTINO";
        state.DestinationTouched = true;
        OracleTables.Recreate(destination, table, columns, log);
        using var writer = new ArrayBatchWriter(destination, table, columns);
        log.Write($"Lote automático={writer.Capacity:N0} filas; fetch=32 MiB; commit por lote.");
        log.Write($"Columnas CLOB/NCLOB={columns.Count(c => c.BindType is OracleDbType.Clob or OracleDbType.NClob)}; " +
            $"ancho de fila reportado por Oracle={reader.RowSize:N0} bytes.");
        foreach (var column in columns)
        {
            log.Write($"COLUMNA {column.Name}: {column.BindType}; origen tamaño={column.Size}; destino={column.SqlType}");
        }
        heartbeat.SetPhase("Leyendo origen e insertando lotes");
        var loadTimer = Stopwatch.StartNew();
        long rows = 0;
        TimeSpan readTime = TimeSpan.Zero;
        TimeSpan insertTime = TimeSpan.Zero;
        var reportTimer = Stopwatch.StartNew();
        while (hasRow)
        {
            cancellation.ThrowIfCancellationRequested();
            state.Stage = "Lectura ORIGEN";
            var readTimer = Stopwatch.StartNew();
            do
            {
                cancellation.ThrowIfCancellationRequested();
                writer.Add(reader);
                hasRow = reader.Read();
            }
            while (hasRow && writer.Count < writer.Capacity);
            readTime += readTimer.Elapsed;
            cancellation.ThrowIfCancellationRequested();
            var writeTimer = Stopwatch.StartNew();
            state.Stage = "Inserción / commit DESTINO";
            rows += writer.Flush();
            heartbeat.Confirmed(rows);
            insertTime += writeTimer.Elapsed;
            if (reportTimer.Elapsed.TotalSeconds >= 10)
            {
                log.Progress(rows, readTime, insertTime, totalTimer.Elapsed, loadTimer.Elapsed);
                reportTimer.Restart();
            }
        }
        cancellation.ThrowIfCancellationRequested();
        loadTimer.Stop();
        heartbeat.SetPhase("Validando conteo destino");
        state.Stage = "Conteo de validación DESTINO";
        using var count = destination.CreateCommand();
        count.CommandTimeout = 600;
        count.CommandText = $"SELECT COUNT(*) FROM {table}";
        long actual = long.Parse(count.ExecuteScalar().ToString()!, CultureInfo.InvariantCulture);
        if (actual != rows)
        {
            throw new InvalidOperationException($"Conteo incorrecto: enviados={rows}, destino={actual}.");
        }
        log.Progress(rows, readTime, insertTime, totalTimer.Elapsed, loadTimer.Elapsed);
        log.Write($"VALIDADO {table}: {actual:N0} filas; total={totalTimer.Elapsed.TotalSeconds:F1}s; " +
            $"primera fila={firstTimer.Elapsed.TotalSeconds:F1}s; transferencia={loadTimer.Elapsed.TotalSeconds:F1}s.");
        state.LoadValidated = true;
        if (daily)
        {
            cancellation.ThrowIfCancellationRequested();
            Phase("DESTINO: retención de tablas diarias; carga validada");
            OracleDailyTables.Cleanup(destination, definition.TablePrefix, period, log, cancellation);
        }
    }
}
