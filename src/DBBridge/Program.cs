using System.Globalization;
using DBBridge.Infrastructure;
using DBBridge.Processes;
using Oracle.ManagedDataAccess.Client;

namespace DBBridge;

internal static class Program
{
    private static int Main()
    {
        string period = DateTime.Today.AddMonths(-1)
            .ToString("yyyyMM", CultureInfo.InvariantCulture);
        ITransferProcess process = new RnipProcess();
        bool acquired = false;
        RunLog? log = null;
        using var cancellation = new CancellationTokenSource();
        using var mutex = new Mutex(false,
            OperatingSystem.IsWindows() ? @"Global\DBBridge_RNIP" : "DBBridge_RNIP");
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
                Console.Error.WriteLine("Ya existe una ejecución RNIP en este equipo.");
                return 2;
            }
            log = new RunLog(process.Name, period);
            log.Write($"INICIO {process.Name}; periodo={period}; versión=1.2");
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
