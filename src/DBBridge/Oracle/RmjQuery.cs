using System.Globalization;
using DBBridge.Infrastructure;

namespace DBBridge.Oracle;

internal static class RmjQuery
{
    public static DateTime Cutoff(string period)
    {
        if (!DateTime.TryParseExact(period + "01", "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime month))
        {
            throw new ArgumentException("Periodo RMJ inválido; se requiere yyyyMM.", nameof(period));
        }

        return month.AddMonths(1);
    }

    public static string Sql => QueryResources.Read("Oracle.RMJ.sql");
}
