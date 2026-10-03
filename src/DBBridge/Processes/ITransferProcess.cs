using DBBridge.Infrastructure;

namespace DBBridge.Processes;

internal interface ITransferProcess
{
    string Name { get; }
    void Execute(string period, RunLog log, CancellationToken cancellation);
}
