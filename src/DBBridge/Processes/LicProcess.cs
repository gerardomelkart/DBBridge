using DBBridge.Infrastructure;
using DBBridge.Oracle;
using Oracle.ManagedDataAccess.Client;

namespace DBBridge.Processes;

internal sealed class LicProcess : ITransferProcess
{
    private static readonly OracleTransferDefinition Definition = new(
        "LIC", "CSNISPLICENCIA", "Z_PYLOAD_LC_",
        "10.251.80.6:1534", "10.106.1.52:1521", 33,
        "Ejecutando consulta de licencias; corte automático según el proceso seleccionado.", Connections.CreateLic, PrepareQuery);
    private readonly bool daily;
    public string Name => daily ? "LIC_D" : "LIC";

    public LicProcess(bool daily = false)
    {
        this.daily = daily;
    }

    public void Execute(string period, RunLog log, CancellationToken cancellation)
    {
        OracleTransfer.Execute(Definition, daily, period, log, cancellation);
    }

    internal static void PrepareQuery(OracleCommand select, string period, bool daily, RunLog log)
    {
        select.CommandText = LicQuery.Sql();
        select.BindByName = true;
        var (start, cutoff) = LicQuery.Range(period, daily);
        select.Parameters.Add("fechaInicio", OracleDbType.Date).Value = start;
        log.Write($"INICIO DE RANGO LIC: {start:yyyy-MM-dd}; filtro sobre LI.FECHA_ACTUALIZA; acumulado del año del corte.");
        select.Parameters.Add("fechaCorteExclusiva", OracleDbType.Date).Value = cutoff;
        log.Write($"CORTE LIC: hasta {cutoff.AddDays(-1):yyyy-MM-dd} inclusive; filtro LI.FECHA_ACTUALIZA < {cutoff:yyyy-MM-dd}; fechas nulas excluidas; revisión LIC=1.15.2.");
    }
}
