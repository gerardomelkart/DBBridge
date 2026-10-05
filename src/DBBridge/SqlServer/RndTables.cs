using System.Globalization;

namespace DBBridge.SqlServer;

internal static class RndTables
{
    private const string Prefix = "tablero_rnd_";

    public static string Name(string date, bool fa) => Prefix + date + (fa ? "_fa" : "");

    public static DateTime? Date(string name, bool fa)
    {
        string suffix = fa ? "_fa" : "";
        if (name.Length != Prefix.Length + 8 + suffix.Length || !name.StartsWith(Prefix, StringComparison.Ordinal)
            || !name.EndsWith(suffix, StringComparison.Ordinal)) return null;
        string date = name.Substring(Prefix.Length, 8);
        if (date.Any(c => c < '0' || c > '9')) return null;
        return DateTime.TryParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed : null;
    }

    public static string? Previous(IEnumerable<string> names, DateTime today, bool fa)
    {
        return names.Where(name => Date(name, fa) is DateTime date && date < today)
            .OrderByDescending(name => name, StringComparer.Ordinal).FirstOrDefault();
    }

    public static string? Oldest(IEnumerable<string> names, DateTime today, bool fa)
    {
        return names.Where(name => Date(name, fa) is DateTime date && date < today)
            .OrderBy(name => name, StringComparer.Ordinal).FirstOrDefault();
    }

    public static string Qualified(string name) => "[RND].[dbo].[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
}
