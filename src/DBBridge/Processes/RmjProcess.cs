using DBBridge.Infrastructure;
using DBBridge.Oracle;
using Oracle.ManagedDataAccess.Client;

namespace DBBridge.Processes;

internal sealed class RmjProcess : ITransferProcess
{
    private static readonly OracleTransferDefinition Definition = new(
        "RMJ", "CSNISPMANDAMIENTOS", "Z_PYLOAD_RMJJ_",
        "10.251.80.6:1531", "10.106.1.52:1521", 29,
        "Ejecutando consulta de mandamientos; corte automático según el proceso seleccionado.", Connections.CreateRmj, PrepareQuery);
    private readonly bool daily;
    public string Name => daily ? "RMJ_D" : "RMJ";

    public RmjProcess(bool daily = false)
    {
        this.daily = daily;
    }

    public void Execute(string period, RunLog log, CancellationToken cancellation)
    {
        OracleTransfer.Execute(Definition, daily, period, log, cancellation);
    }

    internal static void PrepareQuery(OracleCommand select, string period, bool daily, RunLog log)
    {
        select.CommandText = RmjQuery.Sql;
        select.BindByName = true;
        DateTime cutoff = daily ? OracleDailyTables.Cutoff(period) : RmjQuery.Cutoff(period);
        select.Parameters.Add("fechaCorteExclusiva", OracleDbType.Date).Value = cutoff;
        log.Write($"CORTE RMJ: hasta {cutoff.AddDays(-1):yyyy-MM-dd} inclusive; filtro FECHA_REGISTRO < {cutoff:yyyy-MM-dd}; incluye fechas nulas.");
    }
}
