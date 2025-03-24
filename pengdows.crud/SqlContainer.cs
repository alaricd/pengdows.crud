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

    public async Task<int> ExecuteNonQueryAsync(CommandType commandType = CommandType.Text)
    {
        _context.AssertIsWriteConnection();
        DbConnection conn = null;
        DbCommand cmd = null;
        try
        {
            conn = _context.GetConnection(ExecutionType.Write);
            cmd = PrepareCommand(conn);
            cmd.CommandType = commandType;
            return await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            Cleanup(cmd, conn, ExecutionType.Write);
        }
    }

    public async Task<T?> ExecuteScalarAsync<T>(CommandType commandType = CommandType.Text)
    {
        _context.AssertIsReadConnection();
      await using var reader = await ExecuteReaderAsync(commandType);
      if (await reader.ReadAsync().ConfigureAwait(false))
      {
          if (!reader.IsDBNull(0))
          {
              return reader.GetFieldValue<T>(0);
          }
      }

      throw new Exception("No rows returned");
    }

    public async Task<DbDataReader> ExecuteReaderAsync(CommandType commandType = CommandType.Text)
    {
        _context.AssertIsReadConnection();

        DbConnection conn;
        DbCommand cmd = null;
        try
        {
            conn = _context.GetConnection(ExecutionType.Read);
            // unless the databaseContext is in a transaction or SingleConnection mode, 
            // a new connection is returned for every READ operation, therefore, we 
            // are going to set the connection to close and dispose when the reader is 
            // closed. This prevents leaking
            var behavior = (_context is TransactionContext || _context.ConnectionMode == DbMode.SingleConnection)
                ? CommandBehavior.Default
                : CommandBehavior.CloseConnection;
            behavior |= CommandBehavior.SingleResult;
            cmd = conn.CreateCommand();
            cmd.CommandText = Query.ToString();
            cmd.CommandType = commandType;
            cmd.Parameters.AddRange(_parameters.ToArray());
            OpenConnection(conn);
            // if this is our single connection to the database, for a transaction
            //or sqlCe mode, or single connection mode, we will NOT close the connection.
            // otherwise, we will have the connection set to autoclose so that we 
            //close the underlying connection when the DbDataReader is closed;
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
        Console.WriteLine(Query);
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
            _context.CloseAndDisposeConnection(conn);
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