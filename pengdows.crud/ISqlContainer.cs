using System.Data;
using System.Data.Common;
using System.Text;

namespace pengdows.crud;

public interface ISqlContainer
{
    StringBuilder Query { get; }
    DbParameter AppendParameter<T>(string? name, DbType type, T value);
    Task<DbDataReader> ExecuteReaderAsync();
    Task<T?> ExecuteScalarAsync<T>();
    Task<int> ExecuteNonQueryAsync();

}