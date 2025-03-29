using System.Data;
using System.Data.Common;
using System.Text;

namespace pengdows.crud;

public interface ISqlContainer : IDisposable
{
    StringBuilder Query { get; }

    DbParameter AppendParameter<T>(string? name, DbType type, T value);
    void AppendParameters(List<DbParameter> list);
    void AppendParameters(DbParameter parameter);
    Task<int> ExecuteNonQueryAsync(CommandType commandType = CommandType.Text);
    Task<T?> ExecuteScalarAsync<T>(CommandType commandType = CommandType.Text);
    Task<DbDataReader> ExecuteReaderAsync(CommandType commandType = CommandType.Text);
    DbCommand CreateCommand(DbConnection conn);
    void Clear();
}