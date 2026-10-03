using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using DBBridge.Infrastructure;
using DBBridge.Oracle;
using Oracle.ManagedDataAccess.Client;

namespace DBBridge.Processes;

internal sealed class RnipProcess : ITransferProcess
{
    public string Name => "RNIP";

    public void Execute(string period, RunLog log, CancellationToken cancellation)
    {
        string table = $"CSNISPRNIP.G_PYLOAD_RNIP_{period}";
        var connections = Connections.CreateRnip();
        using var origin = connections.Origin;
        using var destination = connections.Destination;
        var totalTimer = Stopwatch.StartNew();
        log.Write("Conectando a origen 10.251.80.6:1531 y destino 10.106.1.52:1521.");
        origin.Open();
        destination.Open();
        log.Write($"Oracle origen={origin.ServerVersion}; destino={destination.ServerVersion}; tabla={table}");
        using var select = origin.CreateCommand();
        select.CommandText = ReadSql();
        select.CommandTimeout = 0; // La consulta de millones puede tardar; Ctrl+C cancela.
        select.FetchSize = 8 * 1024 * 1024;
        using var registration = cancellation.Register(() =>
        {
            try { select.Cancel(); } catch { /* Cancelación de mejor esfuerzo. */ }
        });
        cancellation.ThrowIfCancellationRequested();
        log.Write("Ejecutando consulta. DISTINCT puede demorar la primera fila.");
        var firstTimer = Stopwatch.StartNew();
        using var reader = select.ExecuteReader();
        var columns = ColumnDefinition.Read(reader);
        reader.FetchSize = Math.Max(reader.RowSize, 8L * 1024 * 1024);
        bool hasRow = reader.Read();
        firstTimer.Stop();
        cancellation.ThrowIfCancellationRequested();
        log.Write($"Primera lectura={firstTimer.Elapsed.TotalSeconds:F1}s; columnas={columns.Length}; filas disponibles={hasRow}");
        // Metadatos y primera lectura correctos antes del DROP.
        Recreate(destination, table, columns, log);
        using var writer = new ArrayBatchWriter(destination, table, columns);
        log.Write($"Lote automático={writer.Capacity:N0} filas; fetch=8 MiB; commit por lote.");
        long rows = 0;
        TimeSpan readTime = firstTimer.Elapsed;
        TimeSpan insertTime = TimeSpan.Zero;
        var reportTimer = Stopwatch.StartNew();
        while (hasRow)
        {
            cancellation.ThrowIfCancellationRequested();
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
            rows += writer.Flush();
            insertTime += writeTimer.Elapsed;
            if (reportTimer.Elapsed.TotalSeconds >= 10)
            {
                log.Progress(rows, readTime, insertTime, totalTimer.Elapsed);
                reportTimer.Restart();
            }
        }
        cancellation.ThrowIfCancellationRequested();
        using var count = destination.CreateCommand();
        count.CommandTimeout = 600;
        count.CommandText = $"SELECT COUNT(*) FROM {table}";
        long actual = long.Parse(count.ExecuteScalar().ToString()!, CultureInfo.InvariantCulture);
        if (actual != rows)
            throw new InvalidOperationException($"Conteo incorrecto: enviados={rows}, destino={actual}.");
        log.Progress(rows, readTime, insertTime, totalTimer.Elapsed);
        log.Write($"VALIDADO {table}: {actual:N0} filas; total={totalTimer.Elapsed.TotalSeconds:F1}s.");
    }

    private static string ReadSql()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("DBBridge.Sql.RnipActivos.sql")
            ?? throw new InvalidOperationException("Consulta RNIP no encontrada.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim().TrimEnd(';');
    }

    private static void Recreate(OracleConnection destination, string table,
        ColumnDefinition[] columns, RunLog log)
    {
        using var command = destination.CreateCommand();
        command.CommandTimeout = 120;
        command.CommandText = $"DROP TABLE {table} PURGE";
        try
        {
            command.ExecuteNonQuery();
            log.Write($"Tabla anterior eliminada: {table}");
        }
        catch (OracleException error) when (error.Number == 942)
        {
            log.Write("La tabla del periodo todavía no existe.");
        }
        string definitions = string.Join(",\n    ",
            columns.Select(c => $"\"{c.Name}\" {c.SqlType}"));
        command.CommandText = $"CREATE TABLE {table} (\n    {definitions}\n)";
        command.ExecuteNonQuery();
        log.Write($"Tabla creada: {table}");
    }
}
