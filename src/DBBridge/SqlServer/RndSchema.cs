using System.Data.Common;

namespace DBBridge.SqlServer;

internal static class RndSchema
{
    private static readonly string[] LegacyColumns =
    [
        "ID_DETENIDO", "ID_DETENCION", "ID_PUESTA_DISPOSICION", "ID_DETENIDO_COMPLEMENTO",
        "ID_TRASLADO", "ID_OFICIALES_PSP", "NOMBRE", "APELLIDO_PATERNO",
        "APELLIDO_MATERNO", "EDAD", "FECHA_NACIMIENTO", "SEXO",
        "FECHA_DETENCION", "DESCRIPCION_DETENCION", "MOTIVO_DETENCION", "DESCRIPCION_ESTATUS",
        "DESCRIPCION_ESTATUS_DETENCION", "ENTIDAD", "MUNICIPIO", "COLONIA",
        "CODIGO_POSTAL", "NOMBRE_PAIS", "NOMBRE_FUERO", "NOMBRE_TIPO_TRASLADO",
        "DELITO", "INSTITUCION", "TIPO_DELITO", "NOMBRE_OFICIAL_RECIBE",
        "RANGO_RECIBE", "NOMBRE_FISCALIA", "FECHA_INSERTA", "LESIONES_VISIBLES",
        "DIO_CONTACTO", "RANGO", "NOMBRE_1", "APELLIDO_PATERNO_1",
        "APELLIDO_MATERNO_1", "ADSCRIPCION", "JUSTIFICACION_INCOMPETENCIA", "TIPO_LIBERTAD",
        "INSTITUCION_1", "ADSCRIPCION_1", "PUESTO", "NOMBRE_2",
        "APELLIDO_PATERNO_2", "APELLIDO_MATERNO_2", "ES_BORRADO", "FOLIO_DETENIDO",
        "DESCRIPCION_ESTADO_CIVIL", "CURP", "DESCRIPCION_ESCOLARIDAD", "PROFESION",
        "GRUPO_ETNICO", "NOMBRE_NACIONALIDAD",
    ];

    public static bool IsLegacy(IReadOnlyList<string> columns)
        => columns.SequenceEqual(LegacyColumns, StringComparer.OrdinalIgnoreCase);

    public static (string Sql, List<string> Columns) Create(string target, string template, IReadOnlyList<string> columns, IReadOnlyList<DbColumn> source)
    {
        if (!IsLegacy(columns) || source.Count != 60)
            throw new InvalidOperationException("La adaptación requiere la estructura RND conocida de 54 columnas y una consulta de 60.");
        var expressions = new List<string>();
        var names = new List<string>();

        void Previous(int index)
        {
            names.Add(columns[index]);
            expressions.Add(Quote(columns[index]));
        }

        void Added(int index, string name)
        {
            DbColumn column = source[index];
            if (!string.Equals(column.ColumnName, name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Columna origen inesperada en posición {index + 1}: {column.ColumnName}; se esperaba {name}.");
            names.Add(name);
            expressions.Add($"CAST(NULL AS {StringType(column)}) AS {Quote(name)}");
        }

        for (int i = 0; i < 9; i++) Previous(i);
        Added(9, "NOMBRE_MP");
        Added(10, "APELLIDO_PATERNO_MP");
        Added(11, "APELLIDO_MATERNO_MP");
        for (int i = 9; i < columns.Count; i++) Previous(i);
        Added(57, "ENTIDAD_RESIDENCIA");
        Added(58, "ENTIDAD_NACIMIENTO");
        Added(59, "MUNICIPIO_NACIMIENTO");
        string sql = $"SELECT TOP (0) {string.Join(", ", expressions)} INTO {target} FROM {template};";
        return (sql, names);
    }

    private static string StringType(DbColumn column)
    {
        string type = (column.DataTypeName ?? "").ToLowerInvariant();
        if (type is not ("varchar" or "nvarchar" or "char" or "nchar"))
            throw new InvalidOperationException($"Tipo no admitido para {column.ColumnName}: {column.DataTypeName}.");
        int size = column.ColumnSize ?? throw new InvalidOperationException($"No se recibió longitud de {column.ColumnName}.");
        int limit = type is "nvarchar" or "nchar" ? 4000 : 8000;
        if (size == -1 || size > limit)
        {
            if (type is "char" or "nchar")
                throw new InvalidOperationException($"Longitud no admitida de {column.ColumnName}: {size}.");
            return type + "(max)";
        }
        if (size < 1) throw new InvalidOperationException($"Longitud no admitida de {column.ColumnName}: {size}.");
        return $"{type}({size})";
    }

    private static string Quote(string name) => "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
}

