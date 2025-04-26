using System.Data;
using System.Data.Common;

namespace pengdows.crud.wrappers;

public class TrackedReader : ITrackedReader
{
    private readonly DbDataReader _reader;
    private readonly ITrackedConnection _connection;
    private readonly IAsyncDisposable _connectionLocker;
    private readonly bool _shouldCloseConnection;
    private int _disposed;

    public TrackedReader(DbDataReader reader, 
        ITrackedConnection connection,
        IAsyncDisposable connectionLocker,
        bool shouldCloseConnection)
    {
        _reader = reader;
        _connection = connection;
        _connectionLocker = connectionLocker;
        _shouldCloseConnection = shouldCloseConnection;
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            _reader.Dispose();
            if (_shouldCloseConnection)
            {
                _connection.Close();
            }

            _connectionLocker.DisposeAsync().AsTask().Wait();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            await _reader.DisposeAsync();
            if (_shouldCloseConnection)
            {
                _connection.Close();
            }

            await _connectionLocker.DisposeAsync();
        }
    }

    public async Task<bool> ReadAsync()
    {
        if (await _reader.ReadAsync().ConfigureAwait(false))
        {
            return true;
        }

        await DisposeAsync().ConfigureAwait(false); // Auto-dispose when done reading
        return false;
    }

    public bool Read()
    {
        if (_reader.Read())
        {
            return true;
        }

        Dispose();
        return false;
    }


    public bool GetBoolean(int i) => _reader.GetBoolean(i);
    public byte GetByte(int i) => _reader.GetByte(i);
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => _reader.GetBytes(i, fieldOffset, buffer, bufferoffset, length);
    public char GetChar(int i) => _reader.GetChar(i);
    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => _reader.GetChars(i, fieldoffset, buffer, bufferoffset, length);
    public IDataReader GetData(int i) => _reader.GetData(i);
    public string GetDataTypeName(int i) => _reader.GetDataTypeName(i);
    public DateTime GetDateTime(int i) => _reader.GetDateTime(i);
    public decimal GetDecimal(int i) => _reader.GetDecimal(i);
    public double GetDouble(int i) => _reader.GetDouble(i);
    public Type GetFieldType(int i) => _reader.GetFieldType(i);
    public float GetFloat(int i) => _reader.GetFloat(i);
    public Guid GetGuid(int i) => _reader.GetGuid(i);
    public short GetInt16(int i) => _reader.GetInt16(i);
    public int GetInt32(int i) => _reader.GetInt32(i);
    public long GetInt64(int i) => _reader.GetInt64(i);
    public string GetName(int i) => _reader.GetName(i);
    public int GetOrdinal(string name) => _reader.GetOrdinal(name);
    public string GetString(int i) => _reader.GetString(i);
    public object GetValue(int i) => _reader.GetValue(i);
    public int GetValues(object[] values) => _reader.GetValues(values);
    public bool IsDBNull(int i) => _reader.IsDBNull(i);
    public int FieldCount => _reader.FieldCount;
    public object this[int i] => _reader[i];
    public object this[string name] => _reader[name];
    public void Close() => _reader.Close();
    public DataTable? GetSchemaTable() => _reader.GetSchemaTable();
    public bool NextResult() => false; // No MARS support
    public int Depth => _reader.Depth;
    public bool IsClosed => _reader.IsClosed;
    public int RecordsAffected => _reader.RecordsAffected;
}
