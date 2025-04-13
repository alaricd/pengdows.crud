#region

using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using pengdows.crud.wrappers;

#endregion

namespace pengdows.crud;

public class SqlContainer : ISqlContainer
{
    private readonly IDatabaseContext _context;

    private readonly ILogger<ISqlContainer> _logger;
    private readonly List<DbParameter> _parameters = new();
    private bool _disposed;

    internal SqlContainer(IDatabaseContext context, ILogger<ISqlContainer> logger = null)
    {
        _context = context;
        _logger = logger ?? NullLogger<ISqlContainer>.Instance;
        Query = new StringBuilder("");
    }

    internal SqlContainer(IDatabaseContext context, string? query = "", ILogger<ISqlContainer> logger = null)
    {
        _context = context;
        _logger = logger ?? NullLogger<ISqlContainer>.Instance;
        Query = new StringBuilder(query);
    }

    public StringBuilder Query { get; }

    public int ParameterCount => _parameters.Count;


    public void AddParameter(DbParameter parameter)
    {
        if (parameter == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(parameter.ParameterName))
        {
            parameter.ParameterName = _context.GenerateRandomName();
        }

        _parameters.Add(parameter);
    }

    public DbParameter AddParameterWithValue<T>(DbType type, T value)
    {
        if (value is DbParameter)
        {
            throw new ArgumentException("Parameter type can't be DbParameter.");
        }

        return AddParameterWithValue(null, type, value);
    }

    public DbParameter AddParameterWithValue<T>(string? name, DbType type, T value)
    {
        name ??= _context.GenerateRandomName();
        var parameter = _context.CreateDbParameter(name, type, value);
        _parameters.Add(parameter);
        return parameter;
    }

    public async Task<int> ExecuteNonQueryAsync(CommandType commandType = CommandType.Text)
    {
        _context.AssertIsWriteConnection();
        ITrackedConnection conn = null;
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

        ITrackedConnection conn = null;
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

    public void AddParameters(IEnumerable<DbParameter> list)
    {
        if (list != null && list.Any())
        {
            _parameters.AddRange(list);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public DbCommand CreateCommand(ITrackedConnection conn)
    {
        var cmd = conn.CreateCommand();
        if (_context is TransactionContext transactionContext)
        {
            cmd.Transaction = (transactionContext.Transaction as DbTransaction)
                              ?? throw new InvalidOperationException("Transaction is not a transaction");
        }

        return (cmd as DbCommand)
               ?? throw new InvalidOperationException("Command is not a DbCommand");
    }

    public void Clear()
    {
        Query.Clear();
        _parameters.Clear();
    }

    public string WrapForStoredProc(ExecutionType executionType, bool includeParameters = true)
    {
        var procName = Query.ToString().Trim();

        if (string.IsNullOrWhiteSpace(procName))
            throw new InvalidOperationException("Procedure name is missing from the query.");

        var args = includeParameters ? BuildProcedureArguments() : string.Empty;

        return _context.ProcWrappingStyle switch
        {
            ProcWrappingStyle.PostgreSQL when executionType == ExecutionType.Read
                => $"SELECT * FROM {procName}({args})",

            ProcWrappingStyle.PostgreSQL
                => $"CALL {procName}({args})",

            ProcWrappingStyle.Oracle
                => $"BEGIN\n\t{procName}{(string.IsNullOrEmpty(args) ? string.Empty : $"({args})")};\nEND;",

            ProcWrappingStyle.Exec
                => string.IsNullOrWhiteSpace(args)
                    ? $"EXEC {procName}"
                    : $"EXEC {procName} {args}",

            ProcWrappingStyle.Call
                => $"CALL {procName}({args})",

            ProcWrappingStyle.ExecuteProcedure
                => $"EXECUTE PROCEDURE {procName}({args})",

            _ => throw new NotSupportedException("Stored procedures are not supported by this database.")
        };

        string BuildProcedureArguments()
        {
            if (_parameters.Count == 0)
                return string.Empty;

            // Named parameter support check
            if (_context.DataSourceInfo.SupportsNamedParameters)
            {
                // Trust that dev has set correct names
                return string.Join(", ", _parameters.Select(p => _context.MakeParameterName(p)));
            }

            // Positional binding (e.g., SQLite, MySQL)
            return string.Join(", ", Enumerable.Repeat("?", _parameters.Count));
        }
    }


    private DbCommand PrepareCommand(ITrackedConnection conn, CommandType commandType, ExecutionType executionType)
    {
        if (commandType == CommandType.TableDirect) throw new NotSupportedException("TableDirect isn't supported.");

        OpenConnection(conn);
        var cmd = CreateCommand(conn);
        cmd.CommandType = CommandType.Text;
        _logger.LogInformation(Query.ToString());
        cmd.CommandText = (commandType == CommandType.StoredProcedure)
            ? WrapForStoredProc(executionType)
            : Query.ToString();
        if (_parameters.Count > _context.MaxParameterLimit)
            throw new InvalidOperationException(
                $"Query exceeds the maximum parameter limit of {_context.DataSourceInfo.MaxParameterLimit} for {_context.DataSourceInfo.DatabaseProductName}.");

        if (_parameters.Count > 0)
        {
            cmd.Parameters.AddRange(_parameters.ToArray());
        }

        if (_context.DataSourceInfo.PrepareStatements)
        {
            cmd.Prepare();
        }

        return cmd;
    }

    private void OpenConnection(ITrackedConnection conn)
    {
        if (conn.State != ConnectionState.Open) conn.Open();
    }

    private void Cleanup(DbCommand? cmd, ITrackedConnection? conn, ExecutionType executionType)
    {
        if (cmd != null)
        {
            try
            {
                cmd.Parameters?.Clear();
                cmd.Connection = null;
                cmd.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Command disposal failed: {ex.Message}");
                // We're intentionally not retrying here anymore — disposal failure is generally harmless in this case
            }
        }

        // Don't dispose read connections — they are left open until the reader disposes
        if (executionType == ExecutionType.Read)
            return;

        if (_context is not TransactionContext && conn is not null)
        {
            _context.CloseAndDisposeConnection(conn);
        }
    }


    public string WrapObjectName(string objectName)
    {
        return _context.WrapObjectName(objectName);
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