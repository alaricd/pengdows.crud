using System.Data;
using System.Data.Common;
using System.Text;

namespace pengdows.crud;

public class SqlContainer : ISqlContainer
{
    private readonly IDatabaseContext _context;
    private readonly List<DbParameter> _parameters = new();


    public SqlContainer(IDatabaseContext context)
    {
        _context = context;
        Query = new StringBuilder("");
    }

    public SqlContainer(IDatabaseContext context, string? query = "")
    {
        _context = context;
        Query = new StringBuilder(query);
    }

    public StringBuilder Query { get; }

    public DbParameter AppendParameter<T>(string? name, DbType type, T value)
    {
        name ??= _context.GenerateRandomName();
        var parameter = _context.CreateDbParameter(name, type, value);
        _parameters.Add(parameter);
        return parameter;
    }

    public async Task<int> ExecuteNonQueryAsync()
    {
        return await ExecuteAsync(cmd => cmd.ExecuteNonQueryAsync(), ExecutionType.Write);
    }

    public async Task<T?> ExecuteScalarAsync<T>()
    {
      await using var reader = await ExecuteReaderAsync();
      if (await reader.ReadAsync().ConfigureAwait(false))
      {
          if (!reader.IsDBNull(0))
          {
              return reader.GetFieldValue<T>(0);
          }
      }

      throw new Exception("No rows returned");
    }

    public async Task<DbDataReader> ExecuteReaderAsync()
    {
        DbConnection conn;
        DbCommand cmd = null;
        bool closeConnection = true;
        try
        {
            conn = _context.GetConnection(ExecutionType.Read);
            var c = _context as DatabaseContext;
            var behavior = (_context is TransactionContext || conn == c?.SingleConnection)
                ? CommandBehavior.Default
                : CommandBehavior.CloseConnection;
            behavior |= CommandBehavior.SingleResult;
            cmd = conn.CreateCommand();
            cmd.CommandText = Query.ToString();
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.AddRange(_parameters.ToArray());
            OpenConnection(conn);
            // if this is our single connection to the database, for a transaction
            //or sqlce mode, or single connection mode, we will NOT close the connection.
            // otherwise, we will have the connection set to autoclose so that we 
            //close the underlying connection when the dbdatareader is closed;
            return await cmd.ExecuteReaderAsync(behavior).ConfigureAwait(false);
        }
        finally
        {
            //no matter what we do NOT close the underlying connection
            //or dispose it.
            cmd?.Parameters.Clear();
            //cmd?.Dispose();
        } 
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

    private void Cleanup(DbCommand cmd, DbConnection conn, ExecutionType executionType)
    {
        cmd?.Parameters?.Clear();
        cmd?.Dispose();
        if (executionType == ExecutionType.Read)
            //leave the connection open for reads until we dispose them.
            return;
        if (!(_context is TransactionContext))
        {
            var dbContext = _context as DatabaseContext;
            if (dbContext?.SingleConnection != conn)
            {
                conn.Dispose();
            }
        }
    }

    private async Task<TResult> ExecuteAsync<TResult>(Func<DbCommand, Task<TResult>> action,
        ExecutionType executionType)
    {
        var conn = _context.GetConnection(executionType);

        var cmd = PrepareCommand(conn);
        try
        {
            return await action(cmd).ConfigureAwait(false);
        }
        finally
        {
            Cleanup(cmd, conn, executionType);
        }
    }

    public void AppendParameters(List<DbParameter> list)
    {
        if (list is { Count: > 0 })
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