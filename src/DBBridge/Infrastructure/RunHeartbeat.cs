namespace DBBridge.Infrastructure;

internal sealed class RunHeartbeat : IDisposable
{
    private readonly RunLog log;
    private readonly Timer timer;
    private readonly object gate = new();
    private string phase = "Conectando";
    private long phaseStarted = Environment.TickCount64;
    private long rows;
    private bool disposed;

    public RunHeartbeat(RunLog log)
    {
        this.log = log;
        timer = new Timer(Tick, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
    }

    public void SetPhase(string value)
    {
        lock (gate)
        {
            phase = value;
            phaseStarted = Environment.TickCount64;
        }
    }

    public void Confirmed(long value) => Interlocked.Exchange(ref rows, value);

    private void Tick(object? state)
    {
        lock (gate)
        {
            if (disposed) return;
            double elapsed = (Environment.TickCount64 - phaseStarted) / 1000.0;
            try
            {
                log.Write($"ESTADO: {phase}; tiempo en etapa={elapsed:F0}s; filas confirmadas={Interlocked.Read(ref rows):N0}.");
            }
            catch
            {
                // El temporizador no debe terminar el proceso por un error de escritura.
                // Los mensajes del hilo principal sí propagan el fallo del log.
            }
        }
    }

    public void Dispose()
    {
        lock (gate) disposed = true;
        timer.Dispose();
    }
}
