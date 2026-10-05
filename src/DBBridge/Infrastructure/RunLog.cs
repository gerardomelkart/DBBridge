using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace DBBridge.Infrastructure;

internal sealed class RunLog : IDisposable
{
    private readonly string processName;
    private readonly string directory;
    private readonly Stopwatch timer = Stopwatch.StartNew();
    private readonly object gate = new();
    private readonly Func<DateTimeOffset> clock;
    private StreamWriter? writer;
    private string? currentPath;
    private bool disposed;
    private readonly string runId = $"{Environment.ProcessId}-{Guid.NewGuid():N}";

    public RunLog(string process, string period) : this(process, period, () => DateTimeOffset.Now) { }

    internal RunLog(string process, string period, Func<DateTimeOffset> clock)
    {
        processName = process;
        this.clock = clock;
        directory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(directory);
        Write($"INICIO DE EJECUCIÓN; periodo={period}; archivo diario acumulativo.");
    }

    public void Write(string message)
    {
        lock (gate)
        {
            if (disposed) return;
            DateTimeOffset now = clock();
            string path = Path.Combine(directory, $"{processName}_{now:yyyyMMdd}.log");
            if (path != currentPath)
            {
                writer?.Dispose();
                writer = null;
                var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                currentPath = path;
                Console.WriteLine($"Log diario: {path}");
            }
            string seconds = timer.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture);
            string line = $"{now:yyyy-MM-dd HH:mm:ss zzz} [ejecución={runId}] [+{seconds}s] {message}";
            writer!.WriteLine(line);
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
            writer?.Dispose();
        }
    }
}
