using DBBridge.Infrastructure;
using DBBridge.Processes;

namespace DBBridge;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine($"Uso: DBBridge.exe {ProcessCatalog.Names}. Sin argumentos muestra el menú.");
            return 64;
        }

        bool interactive = args.Length == 0;
        string? selected = interactive ? ProcessMenu.Ask() : args[0].Trim();
        if (selected is null)
        {
            return 0;
        }

        ProcessRegistration? registration = ProcessCatalog.Find(selected);
        int result;
        if (registration is null)
        {
            Console.Error.WriteLine($"Proceso desconocido: '{selected}'. Procesos disponibles: {ProcessCatalog.Names}.");
            result = 64;
        }
        else
        {
            result = ProcessRunner.Run(registration);
        }

        if (interactive && !Console.IsInputRedirected)
        {
            Console.WriteLine();
            Console.Write("Presiona Enter para cerrar...");
            Console.ReadLine();
        }

        return result;
    }
}
