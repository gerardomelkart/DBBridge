using Oracle.ManagedDataAccess.Client;

namespace DBBridge.Oracle;

internal static class OracleErrors
{
    public static string Describe(Exception error, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return "Ejecución cancelada por el usuario.";
        }
        if (error is not OracleException oracle)
        {
            return $"{error.GetType().Name}: {error.Message}";
        }

        string meaning = oracle.Number switch
        {
            1033 => "Oracle informa que la base está iniciando o apagándose y no acepta la conexión. " +
                "Espera unos minutos; si persiste, reporta al administrador de esa base.",
            1017 => "Oracle rechazó el usuario o la contraseña.",
            28000 => "La cuenta de Oracle está bloqueada; requiere revisión del administrador.",
            28001 => "La contraseña de Oracle está vencida.",
            12514 => "El listener no reconoce el SERVICE_NAME solicitado.",
            12505 => "El listener no reconoce el SID solicitado.",
            12541 => "No se encontró un listener disponible en el destino de conexión.",
            12170 => "La conexión a Oracle agotó el tiempo de espera.",
            _ => "Oracle devolvió el error siguiente."
        };
        string original = oracle.Message.Replace("\r", " ").Replace("\n", " ");
        return $"ORA-{oracle.Number:D5}. {meaning} Detalle original: {original}";
    }
}
