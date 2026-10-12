using DBBridge.Processes;

namespace DBBridge.Infrastructure;

internal static class ProcessMenu
{
    public static string? Ask()
    {
        Console.WriteLine("DBBridge - Selección de proceso");
        foreach (var process in ProcessCatalog.All)
        {
            Console.WriteLine($"{process.Option}. {process.Label}");
        }

        Console.WriteLine("0. Salir");
        Console.WriteLine();
        while (true)
        {
            Console.Write("¿Qué proceso deseas ejecutar? ");
            string? input = Console.ReadLine();
            if (input is null || input.Trim() == "0")
            {
                return null;
            }

            ProcessRegistration? process = ProcessCatalog.Find(input);
            if (process is not null)
            {
                return process.Name;
            }

            Console.WriteLine($"Opción inválida. Escribe 1 a {ProcessCatalog.All.Count}, nombre del proceso o 0.");
        }
    }
}
