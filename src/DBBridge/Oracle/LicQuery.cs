using System.Globalization;
using DBBridge.Infrastructure;

namespace DBBridge.Oracle;

internal static class LicQuery
{
    public static DateTime MonthStart(string period)
    {
        if (!DateTime.TryParseExact(period + "01", "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime month))
        {
            throw new ArgumentException("Periodo LIC inválido; se requiere yyyyMM.", nameof(period));
        }

        return month;
    }

    public static string Sql() => QueryResources.Read("Oracle.LIC.sql");

    public static (DateTime Start, DateTime Cutoff) Range(string period, bool daily)
    {
        DateTime cutoff = daily ? OracleDailyTables.RunDate(period) : MonthStart(period).AddMonths(1);
        DateTime lastIncluded = cutoff.AddDays(-1);
        return (new DateTime(lastIncluded.Year, 1, 1), cutoff);
    }
}
