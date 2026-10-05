using System.Globalization;
using DBBridge.Infrastructure;
using DBBridge.Processes;
using Oracle.ManagedDataAccess.Client;

namespace DBBridge;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine("Uso: DBBridge.exe RNIP | RND. Sin argumentos muestra el menú.");
            return 64;
        }

        bool interactive = args.Length == 0;
        string? selected = interactive ? AskProcess() : args[0].Trim().ToUpperInvariant();
        if (selected is null) return 0;
        int result = selected switch
        {
            "RNIP" => RunProcess(new RnipProcess()),
            "RND" => ShowDevelopmentMessage(),
            _ => InvalidProcess(selected)
        };

        if (interactive && !Console.IsInputRedirected)
        {
            Console.WriteLine();
            Console.Write("Presiona Enter para cerrar...");
            Console.ReadLine();
        }
        return result;
    }

    private static string? AskProcess()
    {
        Console.WriteLine("DBBridge - Selección de proceso");
        Console.WriteLine("1. RNIP");
        Console.WriteLine("2. RND (en desarrollo)");
        Console.WriteLine("0. Salir");
        Console.WriteLine();
        while (true)
        {
            Console.Write("¿Qué proceso deseas ejecutar? ");
            string? input = Console.ReadLine();
            if (input is null) return null;
            switch (input.Trim().ToUpperInvariant())
            {
                case "1":
                case "RNIP": return "RNIP";
                case "2":
                case "RND": return "RND";
                case "0": return null;
                default: Console.WriteLine("Opción inválida. Escribe 1, 2, RNIP, RND o 0."); break;
            }
        }
    }

    private static int ShowDevelopmentMessage()
    {
        Console.WriteLine("RND: este módulo está en desarrollo. No se ejecutó ningún proceso.");
        return 4;
    }

    private static int InvalidProcess(string selected)
    {
        Console.Error.WriteLine($"Proceso desconocido: '{selected}'. Procesos disponibles: RNIP y RND.");
        return 64;
    }

    private static int RunProcess(ITransferProcess process)
    {
        string period = DateTime.Today.AddMonths(-1).ToString("yyyyMM", CultureInfo.InvariantCulture);
        bool acquired = false;
        RunLog? log = null;
        using var cancellation = new CancellationTokenSource();
        string mutexName = $"DBBridge_{process.Name}";
        using var mutex = new Mutex(false, OperatingSystem.IsWindows() ? $@"Global\{mutexName}" : mutexName);
        ConsoleCancelEventHandler handler = (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += handler;
        try
        {
            try { acquired = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired)
            {
                Console.Error.WriteLine($"Ya existe una ejecución {process.Name} en este equipo.");
                return 2;
            }
            log = new RunLog(process.Name, period);
            log.Write($"INICIO {process.Name}; periodo={period}; versión=1.3");
            process.Execute(period, log, cancellation.Token);
            log.Write("EXITO: carga finalizada y conteo validado.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            log?.Write("CANCELADO: consulta la etapa y el estado del destino en este log.");
            return 3;
        }
        catch (OracleException) when (cancellation.IsCancellationRequested)
        {
            log?.Write("CANCELADO: consulta la etapa y el estado del destino en este log.");
            return 3;
        }
        catch (OracleException error)
        {
            log?.Write($"FALLO ORACLE {error.Number}: {error.Message}");
            Console.Error.WriteLine($"Error Oracle {error.Number}. Consulta el log.");
            return 1;
        }
        catch (Exception error)
        {
            log?.Write($"FALLO {error.GetType().Name}: {error.Message}");
            Console.Error.WriteLine(error.Message);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
            log?.Dispose();
            if (acquired) mutex.ReleaseMutex();
        }
    }
}
