using System.Data;
using System.Data.Common;
using System.Text;

namespace pengdows.crud;

public class SqlContainer : ISqlContainer
{
    private readonly IDatabaseContext _context;
    private readonly List<DbParameter> _parameters = new();
    private bool _disposed;


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
            cmd = PrepareCommand(conn, commandType, ExecutionType.Write);
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
            var value = reader.GetValue(0); // always returns object
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            return (T?)TypeCoercionHelper.Coerce(value, reader.GetFieldType(0), targetType);
        }

        throw new Exception("No rows returned");
    }

    public async Task<DbDataReader> ExecuteReaderAsync(CommandType commandType = CommandType.Text)
    {
        _context.AssertIsReadConnection();

        DbConnection conn = null;
        DbCommand cmd = null;
        try
        {
            conn = _context.GetConnection(ExecutionType.Read);
            cmd = PrepareCommand(conn, commandType, ExecutionType.Read);

            // unless the databaseContext is in a transaction or SingleConnection mode, 
            // a new connection is returned for every READ operation, therefore, we 
            // are going to set the connection to close and dispose when the reader is 
            // closed. This prevents leaking
            var behavior = _context is TransactionContext || _context.ConnectionMode == DbMode.SingleConnection
                ? CommandBehavior.Default
                : CommandBehavior.CloseConnection;
            //behavior |= CommandBehavior.SingleResult;

            // if this is our single connection to the database, for a transaction
            //or sqlCe mode, or single connection mode, we will NOT close the connection.
            // otherwise, we will have the connection set to autoclose so that we 
            //close the underlying connection when the DbDataReader is closed;
            var dr = await cmd.ExecuteReaderAsync(behavior).ConfigureAwait(false);
            return dr;
        }
        finally
        {
            //no matter what we do NOT close the underlying connection
            //or dispose it.
            Cleanup(cmd, null, ExecutionType.Read);
        }
    }

    public void AppendParameters(List<DbParameter> list)
    {
        if (list is { Count: > 0 }) _parameters.AddRange(list);
    }

    public void AppendParameters(DbParameter parameter)
    {
        if (parameter != null) _parameters.Add(parameter);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public DbCommand CreateCommand(DbConnection conn)
    {
        return conn.CreateCommand();
    }

    public void Clear()
    {
        this.Query.Clear();
        this._parameters.Clear();
    }

    public string WrapForStoredProc(ExecutionType executionType)
    {
        var procText = Query.ToString();

        return _context.ProcWrappingStyle switch
        {
            ProcWrappingStyle.PostgreSQL when executionType == ExecutionType.Read
                => $"SELECT * FROM {procText}",

            ProcWrappingStyle.PostgreSQL
                => $"CALL {procText}",

            ProcWrappingStyle.Oracle
                => $"BEGIN\n{procText};\nEND;",

            ProcWrappingStyle.Exec
                => $"EXEC {procText}",

            ProcWrappingStyle.Call
                => $"CALL {procText}",

            ProcWrappingStyle.ExecuteProcedure
                => $"EXECUTE PROCEDURE {procText}",

            _ => throw new NotSupportedException("Stored procedures are not supported by this database.")
        };
    }

    private DbCommand PrepareCommand(DbConnection conn, CommandType commandType, ExecutionType executionType)
    {
        if (commandType == CommandType.TableDirect) throw new NotSupportedException("TableDirect isn't supported.");

        OpenConnection(conn);
        var cmd = CreateCommand(conn);
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = _context.MissingSqlSettings +
                          (commandType == CommandType.StoredProcedure ? WrapForStoredProc(executionType) : Query);
        if (_parameters.Count > _context.MaxParameterLimit)
            throw new InvalidOperationException(
                $"Query exceeds the maximum parameter limit of {_context.DataSourceInfo.MaxParameterLimit} for {_context.DataSourceInfo.DatabaseProductName}.");

        if (_parameters.Count > 0)
            cmd.Parameters.AddRange(_parameters.ToArray());
        if (_context.DataSourceInfo.PrepareStatements) cmd.Prepare();

        return cmd;
    }

    private void OpenConnection(DbConnection conn)
    {
        if (conn.State != ConnectionState.Open) conn.Open();
    }

    private void Cleanup(DbCommand cmd, DbConnection conn, ExecutionType executionType)
    {
        if (cmd != null)
        {
            cmd.Parameters?.Clear();
            try
            {
                cmd.Connection = null;
                cmd.Dispose();
            }
            catch (Exception ex)
            {
                cmd.Disposed += (_, __) =>
                {
                    Console.WriteLine("Disposed Command that couldn't be cleaned up earlier: " +
                                      _context.DataSourceInfo.DatabaseProductName);
                };
                if (conn != null)
                    conn.Disposed += (_, __) =>
                    {
                        try
                        {
                            cmd?.Dispose();
                        }
                        catch
                        {
                            //eat error quitely
                        }
                    };
            }
        }

        if (executionType == ExecutionType.Read)
            //leave the connection open for reads until we dispose them.
            return;
        if (!(_context is TransactionContext)) _context.CloseAndDisposeConnection(conn);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources here (clear parameters and query)

            _parameters.Clear();
            Query.Clear();
        }

        // Clean up unmanaged resources if necessary (though there may be none in your case)

        _disposed = true;
    }

    ~SqlContainer()
    {
        // Finalizer calls Dispose(false) to clean up unmanaged resources
        Dispose(false);
    }
}