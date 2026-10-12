using DBBridge.Processes;
using Microsoft.Data.SqlClient;
using Oracle.ManagedDataAccess.Client;

namespace DBBridge.Infrastructure;

internal static class ProcessRunner
{
    public static int Run(ProcessRegistration registration)
    {
        ITransferProcess process = registration.Create();
        string period = registration.Period(DateTime.Today);
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
            string detail = registration.Diagnostic;
            log.Write($"INICIO {process.Name}; periodo={period}; versión=1.16; {detail}.");
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
        catch (SqlException) when (cancellation.IsCancellationRequested)
        {
            log?.Write("CANCELADO: consulta la etapa y el estado del destino en este log.");
            return 3;
        }
        catch (SqlException error)
        {
            log?.Write($"FALLO SQL SERVER {error.Number}; estado={error.State}; clase={error.Class}: {error.Message}");
            Console.Error.WriteLine($"Error SQL Server {error.Number}. Consulta el log.");
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
            if (acquired)
            {
                mutex.ReleaseMutex();
            }
        }
    }
}
