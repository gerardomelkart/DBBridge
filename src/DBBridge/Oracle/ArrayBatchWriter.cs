using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace DBBridge.Oracle;

internal sealed class ArrayBatchWriter : IDisposable
{
    private readonly OracleConnection connection;
    private readonly OracleCommand command;
    private readonly ColumnDefinition[] columns;
    private readonly Array[] values;
    private readonly OracleParameterStatus[][] statuses;
    private readonly int[][] sizes;
    public int Capacity { get; }
    public int Count { get; private set; }

    public ArrayBatchWriter(OracleConnection connection, string table,
        ColumnDefinition[] columns)
    {
        this.connection = connection;
        this.columns = columns;
        // Cota estimada de 16 MiB por lote; 5000 filas como máximo.
        long rowBytes = columns.Sum(c => c.BindType is OracleDbType.Decimal
            or OracleDbType.Date or OracleDbType.TimeStamp
            ? 64L : Math.Clamp((long)c.Size * 4, 128, 65536));
        Capacity = (int)Math.Clamp(16L * 1024 * 1024 / Math.Max(rowBytes, 1), 1, 5000);
        values = columns.Select(c => c.CreateValues(Capacity)).ToArray();
        statuses = columns.Select(_ => new OracleParameterStatus[Capacity]).ToArray();
        sizes = columns.Select(_ => new int[Capacity]).ToArray();
        command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = 600;
        string names = string.Join(", ", columns.Select(c => $"\"{c.Name}\""));
        string binds = string.Join(", ", columns.Select(c => $":p{c.Ordinal}"));
        command.CommandText = $"INSERT INTO {table} ({names}) VALUES ({binds})";
        foreach (var column in columns)
        {
            var parameter = command.Parameters.Add($"p{column.Ordinal}", column.BindType);
            parameter.Direction = ParameterDirection.Input;
            parameter.Value = values[column.Ordinal];
        }
    }

    public void Add(OracleDataReader reader)
    {
        if (Count == Capacity) throw new InvalidOperationException("Lote lleno.");
        foreach (var column in columns)
        {
            int ordinal = column.Ordinal;
            bool isNull = reader.IsDBNull(ordinal);
            statuses[ordinal][Count] = isNull
                ? OracleParameterStatus.NullInsert : OracleParameterStatus.Success;
            if (!isNull)
            {
                sizes[ordinal][Count] = column.Fill(reader, values[ordinal], Count);
            }
            else
            {
                // Los tipos Oracle usan el status NullInsert; texto limpia referencias previas.
                if (values[ordinal] is string[] strings) strings[Count] = null!;
                sizes[ordinal][Count] = 1;
            }
        }
        Count++;
    }

    public int Flush()
    {
        if (Count == 0) return 0;
        command.ArrayBindCount = Count;
        foreach (var column in columns)
        {
            var parameter = command.Parameters[column.Ordinal];
            parameter.ArrayBindStatus = statuses[column.Ordinal];
            if (values[column.Ordinal] is string[])
            {
                parameter.ArrayBindSize = sizes[column.Ordinal];
                parameter.Size = Math.Max(1, sizes[column.Ordinal].Take(Count).Max());
            }
        }
        using var transaction = connection.BeginTransaction();
        try
        {
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch
        {
            try { transaction.Rollback(); } catch { /* Conserva el error original. */ }
            throw;
        }
        int written = Count;
        Count = 0;
        foreach (var array in values) Array.Clear(array);
        return written;
    }

    public void Dispose() => command.Dispose();
}
