using DBBridge.Infrastructure;
using Oracle.ManagedDataAccess.Client;

namespace DBBridge.Oracle;

internal sealed record OracleTransferDefinition(
    string Name,
    string Schema,
    string TablePrefix,
    string OriginAddress,
    string DestinationAddress,
    int? ExpectedColumns,
    string QueryMessage,
    Func<(OracleConnection Origin, OracleConnection Destination)> Connect,
    Action<OracleCommand, string, bool, RunLog> PrepareQuery);
