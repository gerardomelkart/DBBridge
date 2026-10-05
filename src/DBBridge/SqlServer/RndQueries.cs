namespace DBBridge.SqlServer;

internal static class RndQueries
{
    public static string Select(bool fa)
    {
        string suffix = fa ? "_fa" : "";
        string hint = fa ? "" : " WITH (NOLOCK)";
        string Table(string name) => $"dbo.[{name}{suffix}]";

        // Mismo orden, relaciones, filtros y multiplicidad que los dos SP originales.
        // RND conserva NOLOCK; RND_FA conserva el aislamiento normal del origen.
        return $"""
            SELECT
                b.ID_DETENIDO, a.ID_DETENCION, c.ID_PUESTA_DISPOSICION,
                d.ID_DETENIDO_COMPLEMENTO, g.ID_TRASLADO, f.ID_OFICIALES_PSP,
                b.NOMBRE AS NOMBRE_PR, b.APELLIDO_PATERNO AS APELLIDO_PATERNO_PR,
                b.APELLIDO_MATERNO AS APELLIDO_MATERNO_PR, d.NOMBRE_DETENIDO AS NOMBRE_MP,
                d.APELLIDO_PATERNO AS APELLIDO_PATERNO_MP, d.APELLIDO_MATERNO AS APELLIDO_MATERNO_MP,
                b.EDAD, b.FECHA_NACIMIENTO, i.SEXO, a.FECHA_DETENCION,
                j.DESCRIPCION_DETENCION, a.MOTIVO_DETENCION, k.DESCRIPCION_ESTATUS,
                l.DESCRIPCION_ESTATUS_DETENCION, m.ENTIDAD AS ENTIDAD_DETENCION,
                n.nombre_municipio_georeferencia AS MUNICIPIO, a.COLONIA, a.CODIGO_POSTAL,
                o.NOMBRE_PAIS, p.NOMBRE_FUERO, q.NOMBRE_TIPO_TRASLADO, g.DELITO,
                r.INSTITUCION, s.TIPO_DELITO, c.NOMBRE_OFICIAL_RECIBE, c.RANGO_RECIBE,
                t.NOMBRE_FISCALIA, b.FECHA_INSERTA,
                CASE b.LESIONES_VISIBLES WHEN 0 THEN 'NO' WHEN 1 THEN 'SI' ELSE NULL END AS LESIONES_VISIBLES,
                CASE b.DIO_CONTACTO WHEN '0' THEN 'NO' WHEN '1' THEN 'SI' ELSE NULL END AS DIO_CONTACTO,
                e.RANGO, e.nombre AS NOMBRE_1, e.apellido_paterno AS APELLIDO_PATERNO_1,
                e.apellido_materno AS APELLIDO_MATERNO_1, e.ADSCRIPCION,
                g.JUSTIFICACION_INCOMPETENCIA, u.TIPO_LIBERTAD,
                f.institucion AS INSTITUCION_1, f.adscripcion AS ADSCRIPCION_1, f.PUESTO,
                f.nombre AS NOMBRE_2, f.paterno AS APELLIDO_PATERNO_2, f.materno AS APELLIDO_MATERNO_2,
                CASE b.ES_BORRADO WHEN '0' THEN 'NO' WHEN '1' THEN 'SI' ELSE NULL END AS ES_BORRADO,
                b.FOLIO_DETENIDO, v.DESCRIPCION_ESTADO_CIVIL, d.CURP, w.DESCRIPCION_ESCOLARIDAD,
                d.PROFESION, d.GRUPO_ETNICO, x.NOMBRE_NACIONALIDAD,
                y.ENTIDAD AS ENTIDAD_RESIDENCIA, z.ENTIDAD AS ENTIDAD_NACIMIENTO,
                za.nombre_municipio_georeferencia AS MUNICIPIO_NACIMIENTO
            FROM {Table("detenciones")} a{hint}
            INNER JOIN {Table("detenidos")} b{hint}
                ON a.id_detencion = b.id_detencion AND a.es_activo = 1 AND b.es_borrado = 0
            LEFT JOIN {Table("puesta_disposiciones")} c{hint}
                ON b.id_detenido = c.id_detenido AND c.es_borrado = 0
            LEFT JOIN {Table("detenidos_datoscomplementarios")} d{hint}
                ON c.id_puesta_disposicion = d.id_puesta_disposicion
            LEFT JOIN {Table("oficiales")} e{hint} ON a.id_detencion = e.id_detencion AND e.es_borrado = 0
            LEFT JOIN {Table("oficiales_PSP")} f{hint} ON a.id_detencion = f.Id_Detencion
            LEFT JOIN {Table("traslados")} g{hint}
                ON d.id_detenido_complemento = g.id_detenido_complemento AND g.es_activo = 1
            LEFT JOIN {Table("traslados_delitos")} h{hint} ON g.id_traslado = h.id_traslado
            LEFT JOIN {Table("cat_sexo")} i{hint} ON b.id_sexo = i.id_sexo
            LEFT JOIN {Table("cat_tipos_detenciones")} j{hint} ON a.id_tipo_detencion = j.id_tipo_detencion
            LEFT JOIN {Table("cat_estatus_detenidos")} k{hint} ON b.id_estatus_detenido = k.id_estatus_detenido
            LEFT JOIN {Table("cat_estatus_detenciones")} l{hint} ON a.id_estatus_detencion = l.id_estatus_detencion
            LEFT JOIN {Table("cat_estados")} m{hint} ON a.id_entidad = m.id_entidad
            LEFT JOIN {Table("municipios_equivalentes")} n{hint}
                ON a.id_municipio = n.id_municipio_georeferencia AND a.id_entidad = n.id_entidad_georeferencia
            LEFT JOIN {Table("cat_paises")} o{hint} ON b.id_pais = o.id_pais
            LEFT JOIN {Table("cat_fueros")} p{hint} ON a.id_fuero = p.id_fuero
            LEFT JOIN {Table("cat_tipos_traslados")} q{hint} ON g.id_tipo_traslado = q.id_tipo_traslado
            LEFT JOIN {Table("cat_instituciones")} r{hint} ON e.id_institucion = r.id_institucion
            LEFT JOIN {Table("cat_tipo_delito")} s{hint} ON h.id_tipo_delito = s.id_tipo_delito
            LEFT JOIN {Table("cat_fiscalias")} t{hint} ON c.id_fiscalia = t.id_fiscalia
            LEFT JOIN {Table("cat_tipos_libertades")} u{hint} ON g.id_tipo_libertad = u.id_tipo_libertad
            LEFT JOIN {Table("cat_estados_civil")} v{hint} ON d.id_estado_civil = v.id_estado_civil
            LEFT JOIN {Table("cat_escolaridades")} w{hint} ON d.id_escolaridad = w.id_escolaridad
            LEFT JOIN {Table("cat_nacionalidades")} x{hint} ON b.id_nacionalidad = x.id_nacionalidad
            LEFT JOIN {Table("cat_estados")} y{hint} ON d.id_entidad = y.id_entidad
            LEFT JOIN {Table("cat_estados")} z{hint} ON d.id_estado = z.id_entidad
            LEFT JOIN {Table("municipios_equivalentes")} za{hint}
                ON d.id_municipio = za.id_municipio_georeferencia AND d.id_estado = za.id_entidad_georeferencia;
            """;
    }
}
