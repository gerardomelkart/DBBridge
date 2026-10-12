using DBBridge.Infrastructure;

namespace DBBridge.SqlServer;

internal static class RndQueries
{
    public static string Select(bool fa) => QueryResources.Read(fa ? "SqlServer.RND_FA.sql" : "SqlServer.RND.sql");
}
