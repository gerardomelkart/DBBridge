using System.Collections;
using System.Diagnostics;
using System.Globalization;
using DBBridge.Infrastructure;
using Microsoft.Data.SqlClient;

namespace DBBridge.SqlServer;

internal sealed class TransferMetrics
{
    private readonly Stopwatch timer = Stopwatch.StartNew();
    private readonly TimeSpan? cpuStart = CpuTime();
    private readonly long allocatedStart = GC.GetTotalAllocatedBytes(false);
    private readonly int[] collections = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];

    public string Summary()
    {
        TimeSpan? cpuNow = CpuTime();
        string cpu = "no disponible";
        if (cpuStart.HasValue && cpuNow.HasValue)
        {
            double seconds = Math.Max(0, (cpuNow.Value - cpuStart.Value).TotalSeconds);
            double percent = seconds / Math.Max(timer.Elapsed.TotalSeconds * Environment.ProcessorCount, 0.001) * 100;
            cpu = $"{seconds:F1}s; uso promedio de CPU del equipo={percent:F1}%";
        }
        long allocated = Math.Max(0, GC.GetTotalAllocatedBytes(false) - allocatedStart);
        return $"CPU consola={cpu}; memoria administrada={GC.GetTotalMemory(false) / 1048576:N0} MiB; " +
            $"memoria total del proceso={WorkingSet()}; asignaciones acumuladas={allocated / 1048576:N0} MiB; " +
            $"GC gen0/1/2={GC.CollectionCount(0) - collections[0]}/{GC.CollectionCount(1) - collections[1]}/{GC.CollectionCount(2) - collections[2]}";
    }

    public static void StartStatistics(SqlConnection connection)
    {
        connection.StatisticsEnabled = true;
        connection.ResetStatistics();
    }

    public static void WriteStatistics(SqlConnection connection, string label, RunLog log, TimeSpan elapsed)
    {
        IDictionary values;
        try { values = connection.RetrieveStatistics(); }
        catch (Exception error)
        {
            log.Write($"MÉTRICAS {label}: no disponibles ({error.GetType().Name}).");
            return;
        }
        log.Write(FormatStatistics(values, label, elapsed));
    }

    internal static string FormatStatistics(IDictionary values, string label, TimeSpan elapsed)
    {
        long? received = Counter(values, "BytesReceived");
        long? sent = Counter(values, "BytesSent");
        string rate = sent.HasValue && received.HasValue
            ? $"{(sent.Value + (double)received.Value) / 1048576 / Math.Max(elapsed.TotalSeconds, 0.001):F2} MiB/s"
            : "no disponible";
        return $"MÉTRICAS {label}: bytes recibidos={Number(received)}; bytes enviados={Number(sent)}; " +
            $"tráfico promedio={rate}; tiempo dentro del proveedor={Seconds(Counter(values, "ExecutionTime"))}; " +
            $"espera de respuestas red/servidor={Seconds(Counter(values, "NetworkServerTime"))}; " +
            $"viajes al servidor={Number(Counter(values, "ServerRoundtrips"))}. " +
            "Son contadores del cliente; no separan tiempo de red y tiempo de ejecución del servidor.";
    }

    private static long? Counter(IDictionary values, string key)
    {
        return values.Contains(key) && values[key] is long value ? value : null;
    }

    private static string Number(long? value) => value?.ToString("N0", CultureInfo.InvariantCulture) ?? "no disponible";
    private static string Seconds(long? value) => value.HasValue ? $"{value.Value / 1000.0:F1}s" : "no disponible";

    private static TimeSpan? CpuTime()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime;
        }
        catch (Exception) { return null; } // Una métrica opcional nunca debe interrumpir la carga.
    }

    private static string WorkingSet()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return $"{process.WorkingSet64 / 1048576:N0} MiB";
        }
        catch (Exception) { return "no disponible"; }
    }
}
