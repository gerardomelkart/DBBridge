using System.Reflection;

namespace DBBridge.Infrastructure;

internal static class QueryResources
{
    public static string Read(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"DBBridge.Queries.{name}")
            ?? throw new InvalidOperationException($"Consulta embebida no encontrada: {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
