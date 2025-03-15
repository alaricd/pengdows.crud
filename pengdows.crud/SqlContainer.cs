using System.Data;
using System.Data.Common;
using System.Text;

namespace pengdows.crud;

public class SqlContainer : ISqlContainer
{
    private readonly IDatabaseContext _context;
    private readonly ITypeMapRegistry _typeMapRegistry;
    private readonly List<DbParameter> _parameters = new();

 
    public SqlContainer(IDatabaseContext context, ITypeMapRegistry typeMapRegistry, string? query = "")
    {
        _context = context;
        _typeMapRegistry = typeMapRegistry;
        Query = new StringBuilder(query);
    }

    public StringBuilder Query { get; }

    public DbParameter AppendParameter<T>(string? name, DbType type, T value)
    {
        name ??= GenerateParameterName();
        var parameter = _context.CreateDbParameter(name, type, value);
        _parameters.Add(parameter);
        return parameter;
    }

    private string GenerateParameterName()
    {
        var dsInfo = _context.DataSourceInfo;
        return $"param{Guid.NewGuid():N}".Substring(0, Math.Min(dsInfo.ParameterNameMaxLength, 8));
    }

    private DbCommand PrepareCommand(DbConnection conn)
    {
        OpenConnection(conn);
        var cmd = conn.CreateCommand();
        cmd.CommandText = _context.MissingSqlSettings + Query;
        if (_parameters.Count > 0)
            cmd.Parameters.AddRange(_parameters.ToArray());
        if (_context.DataSourceInfo.PrepareStatements)
        {
            cmd.Prepare();
        }
        return cmd;
    }

    private void OpenConnection(DbConnection conn)
    {
        if (conn.State != ConnectionState.Open)
        {
            conn.Open();
        }
    }

    private void Cleanup(DbCommand cmd, DbConnection conn)
    {
        cmd?.Parameters?.Clear();
        cmd?.Dispose();
        if (!(_context is TransactionContext))
            conn.Dispose();
    }

    public async Task<int> ExecuteNonQueryAsync()
        => await ExecuteAsync(cmd => cmd.ExecuteNonQueryAsync(), ExecutionType.Write);

    public async Task<T?> ExecuteScalarAsync<T>()
        => await ExecuteAsync(async cmd => (T?)await cmd.ExecuteScalarAsync(), ExecutionType.Read);

    private async Task<TResult> ExecuteAsync<TResult>(Func<DbCommand, Task<TResult>> action, ExecutionType executionType)
    {
        var conn = _context.GetConnection(executionType);
        
        var cmd = PrepareCommand(conn);
        try
        {
            return await action(cmd).ConfigureAwait(false);
        }
        finally
        {
            Cleanup(cmd, conn);
        }
    }

    public async Task<DbDataReader> ExecuteReaderAsync()
        => await ExecuteAsync(cmd => cmd.ExecuteReaderAsync(), ExecutionType.Read);

    public void AppendParameters(List<DbParameter> list)
    {
        if (list != null && list.Count > 0)
        {
            _parameters.AddRange(list);
        }
    }

    public void AppendParameters(DbParameter parameter)
    {
        if (parameter != null)
        {
            _parameters.Add(parameter);
        }
    }
}