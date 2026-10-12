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
FROM dbo.[detenciones_fa] a
INNER JOIN dbo.[detenidos_fa] b
    ON a.id_detencion = b.id_detencion AND a.es_activo = 1 AND b.es_borrado = 0
LEFT JOIN dbo.[puesta_disposiciones_fa] c
    ON b.id_detenido = c.id_detenido AND c.es_borrado = 0
LEFT JOIN dbo.[detenidos_datoscomplementarios_fa] d
    ON c.id_puesta_disposicion = d.id_puesta_disposicion
LEFT JOIN dbo.[oficiales_fa] e ON a.id_detencion = e.id_detencion AND e.es_borrado = 0
LEFT JOIN dbo.[oficiales_PSP_fa] f ON a.id_detencion = f.Id_Detencion
LEFT JOIN dbo.[traslados_fa] g
    ON d.id_detenido_complemento = g.id_detenido_complemento AND g.es_activo = 1
LEFT JOIN dbo.[traslados_delitos_fa] h ON g.id_traslado = h.id_traslado
LEFT JOIN dbo.[cat_sexo_fa] i ON b.id_sexo = i.id_sexo
LEFT JOIN dbo.[cat_tipos_detenciones_fa] j ON a.id_tipo_detencion = j.id_tipo_detencion
LEFT JOIN dbo.[cat_estatus_detenidos_fa] k ON b.id_estatus_detenido = k.id_estatus_detenido
LEFT JOIN dbo.[cat_estatus_detenciones_fa] l ON a.id_estatus_detencion = l.id_estatus_detencion
LEFT JOIN dbo.[cat_estados_fa] m ON a.id_entidad = m.id_entidad
LEFT JOIN dbo.[municipios_equivalentes_fa] n
    ON a.id_municipio = n.id_municipio_georeferencia AND a.id_entidad = n.id_entidad_georeferencia
LEFT JOIN dbo.[cat_paises_fa] o ON b.id_pais = o.id_pais
LEFT JOIN dbo.[cat_fueros_fa] p ON a.id_fuero = p.id_fuero
LEFT JOIN dbo.[cat_tipos_traslados_fa] q ON g.id_tipo_traslado = q.id_tipo_traslado
LEFT JOIN dbo.[cat_instituciones_fa] r ON e.id_institucion = r.id_institucion
LEFT JOIN dbo.[cat_tipo_delito_fa] s ON h.id_tipo_delito = s.id_tipo_delito
LEFT JOIN dbo.[cat_fiscalias_fa] t ON c.id_fiscalia = t.id_fiscalia
LEFT JOIN dbo.[cat_tipos_libertades_fa] u ON g.id_tipo_libertad = u.id_tipo_libertad
LEFT JOIN dbo.[cat_estados_civil_fa] v ON d.id_estado_civil = v.id_estado_civil
LEFT JOIN dbo.[cat_escolaridades_fa] w ON d.id_escolaridad = w.id_escolaridad
LEFT JOIN dbo.[cat_nacionalidades_fa] x ON b.id_nacionalidad = x.id_nacionalidad
LEFT JOIN dbo.[cat_estados_fa] y ON d.id_entidad = y.id_entidad
LEFT JOIN dbo.[cat_estados_fa] z ON d.id_estado = z.id_entidad
LEFT JOIN dbo.[municipios_equivalentes_fa] za
    ON d.id_municipio = za.id_municipio_georeferencia AND d.id_estado = za.id_entidad_georeferencia;
