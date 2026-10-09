using System.Globalization;

namespace DBBridge.Oracle;

internal static class LicQuery
{
    public static DateTime MonthStart(string period)
    {
        if (!DateTime.TryParseExact(period + "01", "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime month))
        {
            throw new ArgumentException("Periodo LIC inválido; se requiere yyyyMM.", nameof(period));
        }

        return month;
    }

    public static string Sql() => Select + "\nWHERE LI.FECHA_ACTUALIZA >= :fechaInicio AND LI.FECHA_ACTUALIZA < :fechaCorteExclusiva";

    public static (DateTime Start, DateTime Cutoff) Range(string period, bool daily)
    {
        DateTime cutoff = daily ? OracleDailyTables.RunDate(period) : MonthStart(period).AddMonths(1);
        DateTime lastIncluded = cutoff.AddDays(-1);
        return (new DateTime(lastIncluded.Year, 1, 1), cutoff);
    }

    private const string Select = """
        SELECT
        LI.ENTIDAD_ID AS ID_ENTIDAD,
        E.NOMBRE AS ENTIDAD,
        LI.MUNICIPIO AS ID_MUNICIPIO,
        M.NOMBRE AS MUNICIPIO,
        LI.OFIEXP AS OFICINA_EXPEDICION,
        trunc(LI.EXPEDICION) AS FECHA_EXPEDICION,
        LI.TIPO_DE_LI AS TIPO_LICENCIA,
        LI.NO_LICENCI,
        trunc(LI.VENCIMIENT) AS VIGENCIA,
        LI.PERIODO,
        LI.CURP,
        LI.RFC,
        LI.APELLIDOPA AS PATERNO,
        LI.APELLIDOMA AS MATERNO,
        LI.NOMBRECOND AS NOMBRE,
            CASE
                 WHEN LI.SEXO = '1' THEN 'MASCULINO'
                 WHEN LI.SEXO = '0' THEN 'FEMENINO'
                 ELSE 'SIN INFORMACION'
            END AS SEXO,
        replace(replace(LI.CALLE,chr(13)||chr(10), ' '), chr(9), ' ') CALLE,
        null as No_EXTERIOR,
        replace(replace(LI.COLONIA, chr(13)||chr(10), ' '), chr(9), ' ')   COLONIA,
        LI.CODPOSTAL AS CP,
        LI.ENTIDAD_CONDUCTOR AS ID_ENTIDAD_DOM,
        E.NOMBRE AS ENTIDAD_DOM,
        LI.MUNICIPIO AS ID_MUNICIPIO_DOM,
        M.NOMBRE AS MUNICIPIO_DOM,
        trunc(LI.FECHANACIM) AS FECHA_NACIMIENTO,
        LI.NACIONALID AS NACIONALIDAD,
        trunc(LI.FECHA_ACTUALIZA) FECHA_ACTUALIZA,
        LI.SANGRE AS TIPO_SANGRE,
        TS.DESCRIPCION AS SANGRE,
              CASE
                 WHEN EXISTS (SELECT 1
                       FROM VHCLADM.IMAGEN_LICENCIA IL
                       WHERE IL.ID_LICENCIA = LI.ID_LICENCIA
                       AND IL.ID_TIPO_IMAGEN=11 AND IL.ID_TIPO_IMAGEN= (SELECT ID_TIPO_IMAGEN FROM VHCLADM.TIPO_IMAGEN TI WHERE TI.ID_TIPO_IMAGEN=IL.ID_TIPO_IMAGEN))
                 THEN 1
                 ELSE 0
              END
                 AS   FOTO_FRENTE,
              CASE
                 WHEN EXISTS (SELECT 1
                       FROM VHCLADM.IMAGEN_LICENCIA IL
                       WHERE IL.ID_LICENCIA = LI.ID_LICENCIA
                       AND IL.ID_TIPO_IMAGEN=12 AND IL.ID_TIPO_IMAGEN= (SELECT ID_TIPO_IMAGEN FROM VHCLADM.TIPO_IMAGEN TI WHERE TI.ID_TIPO_IMAGEN=IL.ID_TIPO_IMAGEN))
                 THEN 1
                 ELSE 0
              END
                 AS   FIRMA,
        
              CASE
                 WHEN EXISTS (SELECT 1
                       FROM VHCLADM.IMAGEN_LICENCIA IL
                       WHERE IL.ID_LICENCIA = LI.ID_LICENCIA
                       AND IL.ID_TIPO_IMAGEN=1 AND IL.ID_TIPO_IMAGEN= (SELECT ID_TIPO_IMAGEN FROM VHCLADM.TIPO_IMAGEN TI WHERE TI.ID_TIPO_IMAGEN=IL.ID_TIPO_IMAGEN))
                 THEN 1
                 ELSE 0
              END
                 AS PULGAR_DER,
        
              CASE
                 WHEN EXISTS (SELECT 1
                       FROM VHCLADM.IMAGEN_LICENCIA IL
                       WHERE IL.ID_LICENCIA = LI.ID_LICENCIA
                       AND IL.ID_TIPO_IMAGEN=2 AND IL.ID_TIPO_IMAGEN= (SELECT ID_TIPO_IMAGEN FROM VHCLADM.TIPO_IMAGEN TI WHERE TI.ID_TIPO_IMAGEN=IL.ID_TIPO_IMAGEN))
                 THEN 1
                 ELSE 0
              END
                 AS INDICE_DER
            from VHCLADM.LICENCIA LI
            LEFT JOIN VHCLADM.ENTIDAD E          ON E.ID_ENTIDAD=LI.ENTIDAD_ID
            LEFT JOIN VHCLADM.MUNICIPIO M        ON M.ID_MUNICIPIO=LI.MUNICIPIO AND M.ID_ENTIDAD=E.ID_ENTIDAD
            LEFT JOIN VHCLADM.TIPO_SANGRE TS     ON TS.ID_TIPO_SANGRE=LI.SANGRE
            LEFT JOIN VHCLADM.SEXO SEX           ON SEX.ID_SEXO=LI.SEXO
        """;
}
