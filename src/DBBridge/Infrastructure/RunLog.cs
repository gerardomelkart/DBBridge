using System.Diagnostics;
using System.Globalization;

namespace DBBridge.Infrastructure;

internal sealed class RunLog : IDisposable
{
    private readonly StreamWriter writer;
    private readonly Stopwatch timer = Stopwatch.StartNew();
    private readonly object gate = new();
    private bool disposed;

    public RunLog(string process, string period)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{process}_{period}_{DateTime.Now:yyyyMMdd_HHmmss}_{Environment.ProcessId}.log");
        writer = new StreamWriter(path, append: false) { AutoFlush = true };
        Write($"Log: {path}");
    }

    public void Write(string message)
    {
        lock (gate)
        {
            if (disposed) return;
            string seconds = timer.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture);
            string line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [+{seconds}s] {message}";
            writer.WriteLine(line);
            Console.WriteLine(line);
        }
    }

    public void Progress(long rows, TimeSpan read, TimeSpan insert, TimeSpan elapsed, TimeSpan loadElapsed)
    {
        using var process = Process.GetCurrentProcess();
        double overallRate = rows / Math.Max(elapsed.TotalSeconds, 0.001);
        double loadRate = rows / Math.Max(loadElapsed.TotalSeconds, 0.001);
        Write($"Filas confirmadas={rows:N0}; carga={loadRate:N0} filas/s; global={overallRate:N0} filas/s; " +
            $"lectura durante carga={read.TotalSeconds:F1}s; inserción+commit={insert.TotalSeconds:F1}s; " +
            $"memoria={process.WorkingSet64 / 1048576:N0} MiB");
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            writer.Dispose();
        }
    }
}
