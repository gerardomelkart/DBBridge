using System.Globalization;

namespace DBBridge.Oracle;

internal static class RmjQuery
{
    public static DateTime Cutoff(string period)
    {
        if (!DateTime.TryParseExact(period + "01", "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime month))
        {
            throw new ArgumentException("Periodo RMJ inválido; se requiere yyyyMM.", nameof(period));
        }

        return month.AddMonths(1);
    }

    public const string Sql = """
        SELECT EM.ID_EMISOR,
               NVL(CF.DESCRIP_FUERO, 'SIN INFORMACION') AS FUERO,
               CJ.DESCRIP_JUZGADO AS NOMBRE_JUZGADO,
               EM.ID_ESTADO AS ID_ENTIDAD,
               CE.DESCRIP_ESTADO AS ENTIDAD,
               CM.DESCRIP_MUNICIPIO AS MUNICIPIO,
               CMA.DESCRIP_TIP_MANDA AS TIPO_MANDATO,
               DE.ID_DELITO,
               CD.DESCRIP_DELITO AS DELITO,
               DE.ID_MODALIDAD,
               NVL(CMO.DESCRIP_MODALIDAD, 'SIN DATO') AS MODALIDAD,
               PR.FECHA_LIBRAMIENTO,
               DG.NOMBRE,
               DG.APATERNO AS PATERNO,
               DG.AMATERNO AS MATERNO,
               CASE PR.ESTADO_PROCESO
                   WHEN 1 THEN 'CANCELADO'
                   WHEN 2 THEN 'CUMPLIDO'
                   WHEN 3 THEN 'EN RESERVA'
                   WHEN 4 THEN 'PENDIENTE'
                   WHEN 5 THEN 'PRESCRITO'
                   WHEN 6 THEN 'VIGENTE'
                   WHEN 7 THEN 'INFORMADA'
                   WHEN 8 THEN 'SUSPENSIÓN TEMPORAL'
                   WHEN 9 THEN 'EN TRAMITE'
                   WHEN 10 THEN 'CUMPLIMENTADA'
                   WHEN 11 THEN 'PARCIALMENTE CUMPLIMENTADA'
                   WHEN 12 THEN 'SUSPENSIÓN DEFINITIVA'
                   WHEN 13 THEN 'CANCELADO POR DUPLICIDAD'
                   WHEN 9999 THEN 'SIN DATO'
                   ELSE 'SIN INFORMACION'
               END AS ESTADO_PROCESO,
               PR.FECHA_CAPTURA,
               EM.FECHA_REGISTRO,
               EM.NO_CONTROL_INST AS LLAVE_EXTERNA,
               EM.ID_INSTITUCION,
               CI.DESCRIP_INSTITU AS INSTITUCION,
               EM.NO_MANDATO,
               PR.NO_AVERIGUACION,
               EM.NO_PROCESO,
               JU.NO_CAUSA,
               DG.EDAD,
               NVL(DG.ALIAS, 'SIN DATO') AS ALIAS,
               CSX.DESCRIP_SEXO AS SEXO,
               CTP.DESCRIP_TIP_PROCE AS TIPO_PROCESO
          FROM MDJDADM.EMISOR EM
          JOIN MDJDADM.DATOS_GENERALES DG ON DG.ID_EMISOR = EM.ID_EMISOR
          JOIN MDJDADM.JUZGADO JU ON JU.ID_EMISOR = EM.ID_EMISOR
          JOIN MDJDADM.PROCESO PR ON PR.ID_DATOS_GENERALES = DG.ID_DATOS_GENERALES
        LEFT JOIN MDJDADM.DELITO DE ON DE.ID_EMISOR = EM.ID_EMISOR
        LEFT JOIN MDJDADM.CAT_DELITOS CD ON CD.ID_DELITO = DE.ID_CAT_DELITO
        LEFT JOIN MDJDADM.CAT_MODALIDAD CMO ON CMO.ID_MODALIDAD = DE.ID_MODALIDAD AND CMO.ID_DELITO = DE.ID_DELITO
        LEFT JOIN MDJDADM.CAT_JUZGADO CJ ON CJ.ID_JUZGADO = JU.ID_CAT_JUZGADO
        LEFT JOIN MDJDADM.CAT_ESTADO CE ON CE.ID_ESTADO = EM.ID_ESTADO
        LEFT JOIN MDJDADM.CAT_MUNICIPIO CM ON CM.ID_MUNICIPIO = EM.ID_MUNICIPIO
        LEFT JOIN MDJDADM.CAT_INSTITUCION CI ON CI.ID_INSTITUCION = EM.ID_INSTITUCION
        LEFT JOIN MDJDADM.CAT_FUERO CF ON CF.CLAVE_FUERO = NVL(PR.FUERO_PROCESO, 9999)
        LEFT JOIN MDJDADM.CAT_TIP_MANDA CMA ON CMA.CLAVE_TIP_MANDA = PR.TIPO_MANDATO
        LEFT JOIN MDJDADM.CAT_TIP_PROCE CTP ON CTP.CLAVE_TIP_PROCE = PR.TIPO_PROCESO
        LEFT JOIN MDJDADM.CAT_SEXO CSX ON CSX.CLAVE_SEXO = DG.SEXO
         WHERE EM.FECHA_REGISTRO < :fechaCorteExclusiva
            OR EM.FECHA_REGISTRO IS NULL
        """;
}
