namespace DBBridge.Security;

internal static partial class Credentials
{
    public static (string Origin, string Destination) Get()
    {
        string origin = "";
        string destination = "";
        Apply(ref origin, ref destination);
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination))
        {
            throw new InvalidOperationException(
                "Falta Credentials.Local.cs. Conserva el archivo incluido en el ZIP " +
                "dentro de src/DBBridge/Security; está excluido de Git.");
        }
        return (origin, destination);
    }

    static partial void Apply(ref string origin, ref string destination);
}
