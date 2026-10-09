using DBBridge.Security;
using Oracle.ManagedDataAccess.Client;

namespace DBBridge.Oracle;

internal static class Connections
{
    public static (OracleConnection Origin, OracleConnection Destination) CreateRnip()
    {
        var passwords = Credentials.Get();
        return (
            Create("USR_CNI", passwords.Origin, "10.251.80.6", 1531,
                "SERVICE_NAME", "drp_crmn"),
            Create("CSNISPRNIP", passwords.Destination, "10.106.1.52", 1521,
                "SID", "BBDDOrac"));
    }

    public static (OracleConnection Origin, OracleConnection Destination) CreateRmj()
    {
        var passwords = Credentials.Get();
        return (
            Create("USR_CNI", passwords.Origin, "10.251.80.6", 1531, "SERVICE_NAME", "drp_crmn"),
            Create("CSNISPMANDAMIENTOS", passwords.Destination, "10.106.1.52", 1521, "SID", "BBDDOrac"));
    }

    public static (OracleConnection Origin, OracleConnection Destination) CreateLic()
    {
        var passwords = Credentials.Get();
        return (
            Create("USR_CNI", passwords.Origin, "10.251.80.6", 1534, "SERVICE_NAME", "drp_vhcl"),
            Create("CSNISPLICENCIA", passwords.Destination, "10.106.1.52", 1521, "SID", "BBDDOrac"));
    }

    private static OracleConnection Create(string user, string password, string host, int port, string connectKind, string database)
    {
        var builder = new OracleConnectionStringBuilder
        {
            UserID = user,
            Password = password,
            DataSource = $"(DESCRIPTION=(CONNECT_TIMEOUT=15)(RETRY_COUNT=0)" +
                $"(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT={port}))" +
                $"(CONNECT_DATA=({connectKind}={database})))",
            Pooling = false,
            Enlist = "false"
        };
        return new OracleConnection(builder.ConnectionString);
    }
}
