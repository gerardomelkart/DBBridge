using Microsoft.Data.SqlClient;

namespace DBBridge.SqlServer;

internal static class SqlConnections
{
    public static SqlConnection CreateOrigin(bool federalAdministrative)
    {
        return Create("10.251.80.136,2078", federalAdministrative ? "RNDetenciones_FA" : "RNDetenciones",
            "USR_CNI", "sjASHD%z2#8mFx$", "DBBridge origen");
    }

    public static SqlConnection CreateDestination()
    {
        return Create("10.106.1.51,1433", "RND", "ADMIN_CNIDT2026", "UnaContraseñaSegura_2026!", "DBBridge destino");
    }

    private static SqlConnection Create(string server, string database, string user, string password, string application)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            UserID = user,
            Password = password,
            Encrypt = SqlConnectionEncryptOption.Optional,
            TrustServerCertificate = true,
            ConnectTimeout = 30,
            ConnectRetryCount = 0,
            ApplicationName = application,
            Pooling = false
        };
        return new SqlConnection(builder.ConnectionString);
    }
}
