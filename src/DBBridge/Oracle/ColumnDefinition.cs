using System.Data;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace DBBridge.Oracle;

internal sealed record ColumnDefinition(int Ordinal, string Name, OracleDbType BindType, string SqlType, int Size)
{
    public static ColumnDefinition[] Read(OracleDataReader reader)
    {
        var schema = reader.GetSchemaTable()
            ?? throw new InvalidOperationException("Oracle no devolvió metadatos.");
        var columns = new List<ColumnDefinition>();
        foreach (DataRow row in schema.Rows)
        {
            int ordinal = Convert.ToInt32(row["ColumnOrdinal"]);
            string name = reader.GetName(ordinal).ToUpperInvariant();
            if (!Regex.IsMatch(name, @"^[A-Z][A-Z0-9_]{0,29}$"))
                throw new InvalidOperationException($"Columna inválida: {name}");
            int size = row["ColumnSize"] == DBNull.Value
                ? 0 : Convert.ToInt32(row["ColumnSize"]);
            var type = (OracleDbType)Convert.ToInt32(row["ProviderType"]);
            (OracleDbType bind, string sql) = type switch
            {
                OracleDbType.Decimal or OracleDbType.Int16 or OracleDbType.Int32
                    or OracleDbType.Int64 or OracleDbType.Byte =>
                    (OracleDbType.Decimal, "NUMBER"),
                OracleDbType.Date => (OracleDbType.Date, "DATE"),
                OracleDbType.TimeStamp => (OracleDbType.TimeStamp, "TIMESTAMP(9)"),
                OracleDbType.Varchar2 or OracleDbType.Char => TextType(size),
                OracleDbType.NVarchar2 or OracleDbType.NChar =>
                    (OracleDbType.NClob, "NCLOB"),
                OracleDbType.Clob => (OracleDbType.Clob, "CLOB"),
                OracleDbType.NClob => (OracleDbType.NClob, "NCLOB"),
                _ => throw new NotSupportedException(
                    $"Tipo {type} no soportado para {name}; tabla destino sin modificar.")
            };
            columns.Add(new ColumnDefinition(ordinal, name, bind, sql, size));
        }
        if (columns.Count == 0 || columns.Select(c => c.Name).Distinct().Count() != columns.Count)
            throw new InvalidOperationException("Columnas vacías o repetidas.");
        return columns.OrderBy(c => c.Ordinal).ToArray();
    }

    private static (OracleDbType, string) TextType(int size)
    {
        // Hasta cuatro bytes por carácter. Evita truncar texto si cambia el charset.
        // Expresiones amplias se conservan en CLOB, sin límite arbitrario de 4000.
        if (size <= 0 || size > 1000) return (OracleDbType.Clob, "CLOB");
        return (OracleDbType.Varchar2, $"VARCHAR2({size * 4} BYTE)");
    }

    public Array CreateValues(int capacity) => BindType switch
    {
        OracleDbType.Decimal => new OracleDecimal[capacity],
        OracleDbType.Date => new OracleDate[capacity],
        OracleDbType.TimeStamp => new OracleTimeStamp[capacity],
        _ => new string[capacity]
    };

    public int Fill(OracleDataReader reader, Array values, int index)
    {
        // Arreglos tipados: evita boxing y reflexión por cada dato numérico/fecha.
        switch (values)
        {
            case OracleDecimal[] numbers:
                numbers[index] = reader.GetOracleDecimal(Ordinal);
                return 0;
            case OracleDate[] dates:
                dates[index] = reader.GetOracleDate(Ordinal);
                return 0;
            case OracleTimeStamp[] timestamps:
                timestamps[index] = reader.GetOracleTimeStamp(Ordinal);
                return 0;
            case string[] texts:
                string text = reader.GetString(Ordinal);
                texts[index] = text;
                return Math.Max(text.Length, 1);
            default:
                throw new NotSupportedException($"Arreglo no soportado: {Name}");
        }
    }
}
