using System.Data;
using System.Diagnostics;
using System.Globalization;
using DBBridge.Infrastructure;
using DBBridge.SqlServer;
using Microsoft.Data.SqlClient;

namespace DBBridge.Processes;

internal sealed class RndProcess : ITransferProcess
{
    private const int BatchSize = 50000;
    private const int NotifyAfter = 10000;
    private readonly bool fa;
    public string Name => fa ? "RND_FA" : "RND";
    public RndProcess(bool fa) => this.fa = fa;

    public void Execute(string period, RunLog log, CancellationToken cancellation)
    {
        ExecuteAsync(period, log, cancellation).GetAwaiter().GetResult();
    }

    private async Task ExecuteAsync(string period, RunLog log, CancellationToken cancellation)
    {
        DateTime today = DateTime.ParseExact(period, "yyyyMMdd", CultureInfo.InvariantCulture);
        string table = RndTables.Name(period, fa);
        string qualified = RndTables.Qualified(table);
        string stage = "Conexión ORIGEN 10.251.80.136:2078";
        bool committed = false;
        bool destinationTouched = false;
        SqlTransaction? transaction = null;
        using var origin = SqlConnections.CreateOrigin(fa);
        using var destination = SqlConnections.CreateDestination();
        using var heartbeat = new RunHeartbeat(log);
        var total = Stopwatch.StartNew();

        void Phase(string value)
        {
            stage = value;
            heartbeat.SetPhase(value);
            log.Write(value);
        }

        try
        {
            Phase($"Conexión ORIGEN 10.251.80.136:2078; base={origin.Database}");
            await origin.OpenAsync(cancellation).ConfigureAwait(false);
            Phase("Conexión DESTINO 10.106.1.51:1433; base=RND");
            await destination.OpenAsync(cancellation).ConfigureAwait(false);
            log.Write($"SQL Server origen={origin.ServerVersion}; destino={destination.ServerVersion}; tabla={qualified}");

            // La transacción conserva la tabla anterior si falla cualquier parte de la carga.
            transaction = (SqlTransaction)await destination.BeginTransactionAsync(cancellation).ConfigureAwait(false);
            Phase($"Bloqueo de ejecución independiente: {Name}");
            using (var command = Command(destination, transaction, """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive',
                    @LockOwner='Transaction', @LockTimeout=0;
                SELECT @result;
                """))
            {
                command.Parameters.Add("@resource", SqlDbType.NVarChar, 255).Value = "DBBridge_" + Name;
                int result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellation).ConfigureAwait(false));
                if (result < 0) throw new InvalidOperationException($"No se obtuvo el bloqueo {Name} (resultado={result}). Puede existir otra ejecución.");
            }

            Phase("Buscando tabla destino y estructura anterior");
            List<string> names = await Tables(destination, transaction, cancellation).ConfigureAwait(false);
            bool exists = names.Contains(table, StringComparer.Ordinal);
            string? previous = RndTables.Previous(names, today, fa);
            if (!exists && previous is null)
                throw new InvalidOperationException($"No existe una tabla anterior válida de {Name} para crear {qualified}. No se modificó el destino.");
            string template = exists ? table : previous!;
            List<string> columns = await Columns(destination, transaction, template, cancellation).ConfigureAwait(false);

            TransferMetrics.StartStatistics(origin);
            Phase($"Consulta ORIGEN {Name}; esperando respuesta");
            using var select = Command(origin, null, RndQueries.Select(fa));
            using var registration = cancellation.Register(() =>
            {
                try { select.Cancel(); }
                catch (SqlException) { }
                catch (InvalidOperationException) { }
            });
            var queryTimer = Stopwatch.StartNew();
            using var source = await select.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellation).ConfigureAwait(false);
            log.Write($"Respuesta de consulta={queryTimer.Elapsed.TotalSeconds:F1}s; columnas={source.FieldCount}.");
            if (source.FieldCount != columns.Count)
                throw new InvalidOperationException($"Estructura incompatible: consulta={source.FieldCount} columnas; tabla {RndTables.Qualified(template)}={columns.Count} columnas insertables. Destino sin modificar.");

            // Los SP insertaban sin lista de columnas: el mapeo conserva ese mismo orden.
            for (int i = 0; i < columns.Count; i++)
                log.Write($"MAPEO {i + 1}: {source.GetName(i)} ({source.GetDataTypeName(i)}) -> {columns[i]}");

            var preparation = Stopwatch.StartNew();
            destinationTouched = true;
            if (exists)
            {
                Phase($"Vaciando tabla existente {qualified}; pendiente de commit");
                string verb = fa ? "TRUNCATE TABLE" : "DELETE FROM";
                await Execute(destination, transaction, $"{verb} {qualified};", cancellation).ConfigureAwait(false);
            }
            else
            {
                Phase($"Creando {qualified} desde {RndTables.Qualified(previous!)}; pendiente de commit");
                await Execute(destination, transaction,
                    $"SELECT TOP (0) * INTO {qualified} FROM {RndTables.Qualified(previous!)};", cancellation).ConfigureAwait(false);
            }
            log.Write($"Preparación del destino={preparation.Elapsed.TotalSeconds:F1}s.");

            Phase($"Carga masiva {Name}: lector SQL nativo; lote={BatchSize}; motor=1.5");
            TransferMetrics.StartStatistics(destination);
            var metrics = new TransferMetrics();
            var load = Stopwatch.StartNew();
            long copied;
            var options = SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.KeepNulls
                | SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.FireTriggers;
            using (var bulk = new SqlBulkCopy(destination, options, transaction))
            {
                bulk.DestinationTableName = qualified;
                bulk.EnableStreaming = true;
                bulk.BatchSize = BatchSize;
                bulk.NotifyAfter = NotifyAfter;
                bulk.BulkCopyTimeout = 0;
                for (int i = 0; i < columns.Count; i++) bulk.ColumnMappings.Add(i, columns[i]);
                long lastProgress = 0;
                bool firstProgress = true;
                bulk.SqlRowsCopied += (_, e) =>
                {
                    e.Abort = cancellation.IsCancellationRequested;
                    long elapsed = load.ElapsedMilliseconds;
                    if (!firstProgress && elapsed - lastProgress < 10000) return;
                    firstProgress = false;
                    lastProgress = elapsed;
                    log.Write($"TRANSFERENCIA: filas procesadas={e.RowsCopied:N0}; pendientes de validación y commit; " +
                        $"ritmo={e.RowsCopied / Math.Max(load.Elapsed.TotalSeconds, 0.001):N0} filas/s; {metrics.Summary()}.");
                };
                // El SqlDataReader directo permite usar la ruta nativa de SqlBulkCopy.
                // El contador del proveedor evita envolver y cronometrar cada una de millones de filas.
                await bulk.WriteToServerAsync(source, cancellation).ConfigureAwait(false);
                copied = bulk.RowsCopied64; // Conteo final exacto, incluyendo el último lote incompleto.
            }
            load.Stop();
            await source.CloseAsync().ConfigureAwait(false);
            log.Write($"TRANSFERENCIA COMPLETA: filas procesadas={copied:N0}; lectura+inserción={load.Elapsed.TotalSeconds:F1}s; " +
                $"ritmo={copied / Math.Max(load.Elapsed.TotalSeconds, 0.001):N0} filas/s; todavía pendiente de commit.");
            log.Write($"MÉTRICAS CONSOLA durante transferencia: {metrics.Summary()}.");
            TransferMetrics.WriteStatistics(origin, "ORIGEN (consulta y lectura)", log, queryTimer.Elapsed);
            TransferMetrics.WriteStatistics(destination, "DESTINO (carga masiva)", log, load.Elapsed);
            origin.StatisticsEnabled = false;
            destination.StatisticsEnabled = false;

            Phase($"Validando conteo en {qualified}");
            using (var count = Command(destination, transaction, $"SELECT COUNT_BIG(*) FROM {qualified};"))
            {
                long actual = Convert.ToInt64(await count.ExecuteScalarAsync(cancellation).ConfigureAwait(false));
                if (actual != copied)
                    throw new InvalidOperationException($"Conteo incorrecto: filas procesadas={copied:N0}; destino={actual:N0}. Se revertirá la carga.");
            }

            string? oldest = RndTables.Oldest(names, today, fa);
            if (oldest is not null)
            {
                Phase($"Carga validada; borrando tabla más antigua {RndTables.Qualified(oldest)}; pendiente de commit");
                await Execute(destination, transaction, $"DROP TABLE {RndTables.Qualified(oldest)};", cancellation).ConfigureAwait(false);
            }
            else log.Write($"No hay una tabla anterior de {Name} que borrar; se conserva la tabla de hoy.");

            cancellation.ThrowIfCancellationRequested();
            Phase("Confirmando transacción de carga y limpieza");
            var commit = Stopwatch.StartNew();
            // Una vez iniciado el commit, no se interrumpe para evitar un resultado ambiguo por Ctrl+C.
            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            committed = true;
            heartbeat.Confirmed(copied);
            log.Write(exists ? $"TABLA VACIADA Y RECARGADA: {qualified}."
                : $"TABLA CREADA: {qualified}; estructura copiada de {RndTables.Qualified(previous!)}.");
            if (oldest is not null) log.Write($"TABLA BORRADA: {RndTables.Qualified(oldest)}; era la más antigua de {Name}.");
            log.Write($"CARGA CONFIRMADA: tabla={qualified}; filas={copied:N0}; commit={commit.Elapsed.TotalSeconds:F1}s; total={total.Elapsed.TotalSeconds:F1}s.");
        }
        catch (Exception error)
        {
            log.Write($"FALLO EN {stage}: {error.GetType().Name}: {error.Message}");
            if (!committed && transaction is not null)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    log.Write(destinationTouched ? "ROLLBACK CONFIRMADO: carga y limpieza revertidas; destino conservado."
                        : "DESTINO SIN MODIFICAR: no comenzó el vaciado ni la creación.");
                }
                catch (Exception rollbackError)
                {
                    log.Write($"No se pudo confirmar el rollback desde la consola: {rollbackError.Message}. Revisar estado de {qualified}; no se confirmó la carga.");
                }
            }
            else if (!committed) log.Write("DESTINO SIN MODIFICAR: no comenzó el vaciado ni la creación.");
            else log.Write("La transacción ya quedó confirmada; el fallo ocurrió después del commit.");
            if (cancellation.IsCancellationRequested && !committed) throw new OperationCanceledException("Proceso cancelado.", error, cancellation);
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static SqlCommand Command(SqlConnection connection, SqlTransaction? transaction, string sql)
    {
        return new SqlCommand(sql, connection, transaction) { CommandTimeout = 0 };
    }

    private static async Task Execute(SqlConnection connection, SqlTransaction transaction, string sql, CancellationToken cancellation)
    {
        using var command = Command(connection, transaction, sql);
        await command.ExecuteNonQueryAsync(cancellation).ConfigureAwait(false);
    }

    private static async Task<List<string>> Tables(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellation)
    {
        using var command = Command(connection, transaction, "SELECT name FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo') AND name LIKE 'tablero[_]rnd[_]%';");
        using var reader = await command.ExecuteReaderAsync(cancellation).ConfigureAwait(false);
        var result = new List<string>();
        while (await reader.ReadAsync(cancellation).ConfigureAwait(false)) result.Add(reader.GetString(0));
        return result;
    }

    private static async Task<List<string>> Columns(SqlConnection connection, SqlTransaction transaction, string table, CancellationToken cancellation)
    {
        using var command = Command(connection, transaction, """
            SELECT name FROM sys.columns
            WHERE object_id=OBJECT_ID(@table) AND is_identity=0 AND is_computed=0 AND system_type_id<>189
            ORDER BY column_id;
            """);
        command.Parameters.Add("@table", SqlDbType.NVarChar, 300).Value = RndTables.Qualified(table);
        using var reader = await command.ExecuteReaderAsync(cancellation).ConfigureAwait(false);
        var result = new List<string>();
        while (await reader.ReadAsync(cancellation).ConfigureAwait(false)) result.Add(reader.GetString(0));
        return result;
    }
}
