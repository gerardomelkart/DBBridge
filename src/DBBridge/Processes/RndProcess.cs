using DBBridge.Infrastructure;
using DBBridge.SqlServer;

namespace DBBridge.Processes;

internal sealed class RndProcess : ITransferProcess
{
    private readonly RndTransfer transfer;
    public string Name => transfer.Name;

    public RndProcess(bool fa)
    {
        transfer = new RndTransfer(fa);
    }

    public void Execute(string period, RunLog log, CancellationToken cancellation)
    {
        transfer.Execute(period, log, cancellation);
    }
}
