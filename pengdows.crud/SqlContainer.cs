
using System.Data;
using System.Data.Common;
using System.Text;

namespace pengdows.crud;

public class SqlContainer
{
    private readonly IDbContext _context;
    private readonly List<DbParameter> _parameters = new();

    public SqlContainer(IDbContext context, string query = "")
    {
        _context = context;
        Query = new StringBuilder(query);
    }

    public StringBuilder Query { get; }

    public DbParameter AppendParameter<T>(string? name, DbType type, T value)
    {
        var dsInfo = _context.DataSourceInfo;
        
        if (string.IsNullOrEmpty(name))
        {
            name = "param" + Guid.NewGuid().ToString("N").Substring(0, Math.Min(dsInfo.ParameterNameMaxLength, 16));
            if (!dsInfo.ParameterNamePatternRegex.IsMatch(name))
                throw new InvalidOperationException($"Generated parameter name '{name}' is invalid.");
        }

        var parameter = _context.CreateDbParameter(name, type, value);
        _parameters.Add(parameter);
        return parameter;
    }

    public async Task<DbDataReader> ExecuteReaderAsync()
    {
        var conn = _context.GetConnection(ExecutionType.Read);
  \
        try
        {
           var cmd = conn.CreateCommand();
            cmd.CommandText = Query.ToString();

            if (_parameters.Count > 0)
                cmd.Parameters.AddRange(_parameters.ToArray());

            var behavior = CommandBehavior.SequentialAccess | CommandBehavior.SingleResult;
            if (!(_context is TransactionContext))
                behavior |= CommandBehavior.CloseConnection;

            return await cmd.ExecuteReaderAsync(behavior);
        }
        catch
        {
            CloseConnection(conn);
            throw;
        }
    }

    public async Task<T?> ExecuteScalarAsync<T>()
    {
        await using var reader = await ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            if (await reader.IsDBNullAsync(0))
                return default;
            
            var value = await reader.GetFieldValueAsync<object>(0);
            return value is T typedValue ? typedValue : (T)Convert.ChangeType(value, typeof(T));
        }
        return default;
    }

    public async Task<int> ExecuteNonQueryAsync()
    {
        var conn = _context.GetConnection(ExecutionType.Write);
    
        try
        {
           var cmd = conn.CreateCommand();
            cmd.CommandText = Query.ToString();

            if (_parameters.Count > 0)
                cmd.Parameters.AddRange(_parameters.ToArray());

            return await cmd.ExecuteNonQueryAsync();
        }
       
        finally
        {
            CloseConnection(conn);
        }
    }

    private void CloseConnection(DbConnection conn)
    {
        if (_context is TransactionContext)
            return;

        switch (_context.ConnectionMode)
        {
            case DbMode.Standard:
            case DbMode.SqlExpressUserMode:
                conn.Dispose();
                break;
            default:
                if (conn != _context.Connection)
                {
                    conn.Dispose();
                }
                break;
        }
    }
}
