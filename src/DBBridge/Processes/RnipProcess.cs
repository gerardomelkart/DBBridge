using DBBridge.Infrastructure;
using DBBridge.Oracle;
using Oracle.ManagedDataAccess.Client;

namespace DBBridge.Processes;

internal sealed class RnipProcess : ITransferProcess
{
    private static readonly OracleTransferDefinition Definition = new(
        "RNIP", "CSNISPRNIP", "Z_PYLOAD_RNIP_",
        "10.251.80.6:1531", "10.106.1.52:1521", null,
        "Ejecutando consulta. DISTINCT puede demorar la primera fila.", Connections.CreateRnip, PrepareQuery);
    private readonly bool daily;
    public string Name => daily ? "RNIP_D" : "RNIP";

    public RnipProcess(bool daily = false)
    {
        this.daily = daily;
    }

    public void Execute(string period, RunLog log, CancellationToken cancellation)
    {
        OracleTransfer.Execute(Definition, daily, period, log, cancellation);
    }

    internal static void PrepareQuery(OracleCommand select, string period, bool daily, RunLog log)
    {
        select.CommandText = QueryResources.Read("Oracle.RNIP.sql").TrimEnd(';');
        if (daily)
        {
            DateTime cutoff = OracleDailyTables.Cutoff(period);
            select.CommandText += "\nWHERE (EM.FECHA_REGISTRO < :fechaCorteExclusiva OR EM.FECHA_REGISTRO IS NULL)";
            select.BindByName = true;
            select.Parameters.Add("fechaCorteExclusiva", OracleDbType.Date).Value = cutoff;
            log.Write($"CORTE DIARIO RNIP: hasta {cutoff.AddDays(-1):yyyy-MM-dd} inclusive; filtro sobre EM.FECHA_REGISTRO; incluye fechas nulas.");
        }
    }
}
