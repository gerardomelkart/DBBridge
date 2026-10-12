using DBBridge.Infrastructure;
using Oracle.ManagedDataAccess.Client;

namespace DBBridge.Oracle;

internal static class OracleTables
{
    public static void Recreate(OracleConnection destination, string table, ColumnDefinition[] columns, RunLog log)
    {
        using var command = destination.CreateCommand();
        command.CommandTimeout = 120;
        command.CommandText = $"DROP TABLE {table} PURGE";
        try
        {
            command.ExecuteNonQuery();
            log.Write($"Tabla anterior eliminada: {table}");
        }
        catch (OracleException error) when (error.Number == 942)
        {
            log.Write("La tabla del periodo todavía no existe.");
        }
        string definitions = string.Join(",\n    ",
            columns.Select(c => $"\"{c.Name}\" {c.SqlType}"));
        command.CommandText = $"CREATE TABLE {table} (\n    {definitions}\n)";
        command.ExecuteNonQuery();
        log.Write($"Tabla creada: {table}");
    }
}
