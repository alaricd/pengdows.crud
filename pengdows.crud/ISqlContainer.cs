#region

using System.Data;
using System.Data.Common;
using System.Text;

#endregion

namespace pengdows.crud;

public interface ISqlContainer : IDisposable
{
    StringBuilder Query { get; }
    int ParameterCount { get; }

    DbParameter AppendParameter<T>(DbType type, T value);
    DbParameter AppendParameter<T>(string? name, DbType type, T value);
    void AppendParameters(List<DbParameter> list);
    void AppendParameters(DbParameter parameter);
    Task<int> ExecuteNonQueryAsync(CommandType commandType = CommandType.Text);
    Task<T?> ExecuteScalarAsync<T>(CommandType commandType = CommandType.Text);
    Task<DbDataReader> ExecuteReaderAsync(CommandType commandType = CommandType.Text);
    DbCommand CreateCommand(DbConnection conn);
    void Clear();
}