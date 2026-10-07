using System.Globalization;
using DBBridge.Infrastructure;
using Oracle.ManagedDataAccess.Client;

namespace DBBridge.Oracle;

internal static class OracleDailyTables
{
    public const int MinimumRetainedTables = 31;
    internal sealed record RetentionPlan(int Before, int After, string? ToDelete);

    public static DateTime RunDate(string period)
    {
        if (!DateTime.TryParseExact(period, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
        {
            throw new ArgumentException("Periodo diario inválido; se requiere yyyyMMdd.", nameof(period));
        }

        return date;
    }

    public static DateTime Cutoff(string period) => RunDate(period).AddDays(1);

    public static string TableName(string prefix, string period)
    {
        ValidatePrefix(prefix);
        RunDate(period);
        return $"{prefix}{period}D";
    }

    public static RetentionPlan Retention(IEnumerable<string> names, string prefix, DateTime today)
    {
        ValidatePrefix(prefix);
        var eligible = names.Distinct(StringComparer.Ordinal)
            .Select(name => (Name: name, Date: TableDate(name, prefix)))
            .Where(table => table.Date.HasValue && table.Date.Value <= today.Date)
            .OrderBy(table => table.Date)
            .ToList();
        string? oldest = eligible.Count > MinimumRetainedTables
            ? eligible.FirstOrDefault(table => table.Date < today.Date).Name
            : null;
        return new RetentionPlan(eligible.Count, eligible.Count - (oldest is null ? 0 : 1), oldest);
    }

    public static void Cleanup(OracleConnection destination, string prefix, string period, RunLog log, CancellationToken cancellation)
    {
        ValidatePrefix(prefix);
        var names = new List<string>();
        using (var command = destination.CreateCommand())
        {
            command.BindByName = true;
            command.CommandTimeout = 120;
            command.CommandText = "SELECT TABLE_NAME FROM USER_TABLES WHERE SUBSTR(TABLE_NAME, 1, :prefixLength) = :prefix";
            command.Parameters.Add("prefixLength", OracleDbType.Int32).Value = prefix.Length;
            command.Parameters.Add("prefix", OracleDbType.Varchar2).Value = prefix;
            using var registration = cancellation.Register(() =>
            {
                try { command.Cancel(); } catch { /* Conserva la cancelación original. */ }
            });
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                cancellation.ThrowIfCancellationRequested();
                names.Add(reader.GetString(0));
            }
        }

        var plan = Retention(names, prefix, RunDate(period));
        if (plan.ToDelete is null)
        {
            log.Write($"RETENCIÓN DIARIA: tablas={plan.Before}; mínimo={MinimumRetainedTables}; sin borrar tablas anteriores.");
            return;
        }

        cancellation.ThrowIfCancellationRequested();
        using var drop = destination.CreateCommand();
        drop.CommandTimeout = 120;
        drop.CommandText = $"DROP TABLE \"{plan.ToDelete}\" PURGE";
        using var dropRegistration = cancellation.Register(() =>
        {
            try { drop.Cancel(); } catch { /* Conserva la cancelación original. */ }
        });
        log.Write($"RETENCIÓN DIARIA: tablas={plan.Before}; eliminando la más antigua después de validar la carga: {plan.ToDelete}.");
        drop.ExecuteNonQuery();
        log.Write($"TABLA DIARIA BORRADA: {plan.ToDelete}; tablas conservadas={plan.After}; mínimo={MinimumRetainedTables}.");
    }

    private static DateTime? TableDate(string name, string prefix)
    {
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Length != prefix.Length + 9 || !name.EndsWith('D'))
        {
            return null;
        }

        string date = name.Substring(prefix.Length, 8);
        return DateTime.TryParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
            ? parsed
            : null;
    }

    private static void ValidatePrefix(string prefix)
    {
        if (prefix is not "Z_PYLOAD_RNIP_" and not "Z_PYLOAD_RMJJ_")
        {
            throw new ArgumentException("Serie diaria Oracle no reconocida.", nameof(prefix));
        }
    }
}
