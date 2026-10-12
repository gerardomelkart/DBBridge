using System.Globalization;

namespace DBBridge.Processes;

internal sealed record ProcessRegistration(
    int Option,
    string Name,
    string Label,
    bool Monthly,
    bool Oracle,
    Func<ITransferProcess> Create)
{
    public string Diagnostic => Oracle ? "diagnóstico Oracle por etapas" : "motor de carga SQL=1.7; validación de estructura";

    public string Period(DateTime today) => Monthly
        ? today.AddMonths(-1).ToString("yyyyMM", CultureInfo.InvariantCulture)
        : today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
}

internal static class ProcessCatalog
{
    public static IReadOnlyList<ProcessRegistration> All { get; } = Array.AsReadOnly(new[]
    {
        new ProcessRegistration(1, "RNIP", "RNIP", true, true, () => new RnipProcess()),
        new ProcessRegistration(2, "RND", "RND", false, false, () => new RndProcess(false)),
        new ProcessRegistration(3, "RND_FA", "RND_FA", false, false, () => new RndProcess(true)),
        new ProcessRegistration(4, "RMJ", "RMJ (Mandamientos)", true, true, () => new RmjProcess()),
        new ProcessRegistration(5, "RNIP_D", "RNIP_D (Corte diario)", false, true, () => new RnipProcess(true)),
        new ProcessRegistration(6, "RMJ_D", "RMJ_D (Mandamientos diario)", false, true, () => new RmjProcess(true)),
        new ProcessRegistration(7, "LIC", "LIC (Licencias mensual)", true, true, () => new LicProcess()),
        new ProcessRegistration(8, "LIC_D", "LIC_D (Licencias diario)", false, true, () => new LicProcess(true))
    });

    public static string Names => string.Join(" | ", All.Select(process => process.Name));

    public static ProcessRegistration? Find(string input)
    {
        string value = input.Trim();
        return All.FirstOrDefault(process => process.Name.Equals(value, StringComparison.OrdinalIgnoreCase)
            || process.Option.ToString(CultureInfo.InvariantCulture) == value);
    }
}
