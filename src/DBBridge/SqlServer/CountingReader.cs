using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace DBBridge.SqlServer;

internal sealed class CountingReader : DbDataReader
{
    private readonly DbDataReader reader;
    private readonly Action firstRow;
    private long rows;
    private long readTicks;

    public CountingReader(DbDataReader reader, Action firstRow)
    {
        this.reader = reader;
        this.firstRow = firstRow;
    }

    public long Rows => Interlocked.Read(ref rows);
    public TimeSpan ReadTime => TimeSpan.FromSeconds(Interlocked.Read(ref readTicks) / (double)Stopwatch.Frequency);

    private bool Record(bool result, long started)
    {
        Interlocked.Add(ref readTicks, Stopwatch.GetTimestamp() - started);
        if (result && Interlocked.Increment(ref rows) == 1) firstRow();
        return result;
    }

    public override bool Read()
    {
        long started = Stopwatch.GetTimestamp();
        return Record(reader.Read(), started);
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();
        return Record(await reader.ReadAsync(cancellationToken).ConfigureAwait(false), started);
    }

    public override object this[int ordinal] => reader[ordinal];
    public override object this[string name] => reader[name];
    public override int Depth => reader.Depth;
    public override int FieldCount => reader.FieldCount;
    public override bool HasRows => reader.HasRows;
    public override bool IsClosed => reader.IsClosed;
    public override int RecordsAffected => reader.RecordsAffected;
    public override int VisibleFieldCount => reader.VisibleFieldCount;
    public override bool GetBoolean(int ordinal) => reader.GetBoolean(ordinal);
    public override byte GetByte(int ordinal) => reader.GetByte(ordinal);
    public override long GetBytes(int ordinal, long offset, byte[]? buffer, int bufferOffset, int length)
        => reader.GetBytes(ordinal, offset, buffer, bufferOffset, length);
    public override char GetChar(int ordinal) => reader.GetChar(ordinal);
    public override long GetChars(int ordinal, long offset, char[]? buffer, int bufferOffset, int length)
        => reader.GetChars(ordinal, offset, buffer, bufferOffset, length);
    public override string GetDataTypeName(int ordinal) => reader.GetDataTypeName(ordinal);
    public override DateTime GetDateTime(int ordinal) => reader.GetDateTime(ordinal);
    public override decimal GetDecimal(int ordinal) => reader.GetDecimal(ordinal);
    public override double GetDouble(int ordinal) => reader.GetDouble(ordinal);
    public override Type GetFieldType(int ordinal) => reader.GetFieldType(ordinal);
    public override float GetFloat(int ordinal) => reader.GetFloat(ordinal);
    public override Guid GetGuid(int ordinal) => reader.GetGuid(ordinal);
    public override short GetInt16(int ordinal) => reader.GetInt16(ordinal);
    public override int GetInt32(int ordinal) => reader.GetInt32(ordinal);
    public override long GetInt64(int ordinal) => reader.GetInt64(ordinal);
    public override string GetName(int ordinal) => reader.GetName(ordinal);
    public override int GetOrdinal(string name) => reader.GetOrdinal(name);
    public override string GetString(int ordinal) => reader.GetString(ordinal);
    public override object GetValue(int ordinal) => reader.GetValue(ordinal);
    public override int GetValues(object[] values) => reader.GetValues(values);
    public override bool IsDBNull(int ordinal) => reader.IsDBNull(ordinal);
    public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken)
        => reader.IsDBNullAsync(ordinal, cancellationToken);
    public override T GetFieldValue<T>(int ordinal) => reader.GetFieldValue<T>(ordinal);
    public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken)
        => reader.GetFieldValueAsync<T>(ordinal, cancellationToken);
    public override Stream GetStream(int ordinal) => reader.GetStream(ordinal);
    public override TextReader GetTextReader(int ordinal) => reader.GetTextReader(ordinal);
    public override DataTable? GetSchemaTable() => reader.GetSchemaTable();
    public override IEnumerator GetEnumerator() => new DbEnumerator(this, false);
    public override bool NextResult() => reader.NextResult();
    public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => reader.NextResultAsync(cancellationToken);
    public override void Close() => reader.Close();
    protected override void Dispose(bool disposing)
    {
        if (disposing) reader.Dispose();
        base.Dispose(disposing);
    }
}
