using System.Data;
using System.Data.Common;

namespace pengdows.crud;

public class TransactionContext : ITransactionContext
{
    private readonly DbConnection _connection;
    private readonly IDatabaseContext _context;
    private readonly DbTransaction _transaction;
    private bool _committed;
    private long _disposed;
    private bool _isCompleted;
    private bool _rolledBack;

    public TransactionContext(IDatabaseContext context, IsolationLevel isolationLevel)
    {
        _context = context;
        _connection = _context.GetConnection(ExecutionType.Write);
        EnsureConnectionIsOpen();
        _transaction = _connection.BeginTransaction();
    }

    private bool IsCompleted
    {
        get
        {
            _isCompleted |= _committed || _rolledBack;
            return _isCompleted;
        }
    }


    public ISqlContainer CreateSqlContainer(string? query = null)
    {
        if (IsCompleted)
            throw new Exception("Cannot create a sql container because this transaction is already completed.");
        return new SqlContainer(this, query);
    }

    public DbParameter CreateDbParameter<T>(string name, DbType type, T value)
    {
        return _context.CreateDbParameter(name, type, value);
    }

    public DbConnection GetConnection(ExecutionType type)
    {
        return _connection;
    }

    public string WrapObjectName(string name)
    {
        return _context.WrapObjectName(name);
    }

    public TransactionContext BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        throw new NotImplementedException(
            "TransactionContext cannot start a new transaction, nested transactions are not supported.");
    }

    public string GenerateRandomName(int length = 8)
    {
        return _context.GenerateRandomName(length);
    }

    public DbParameter CreateDbParameter<T>(DbType type, T value)
    {
        return _context.CreateDbParameter(type, value);
    }

    public void AssertIsReadConnection()
    {
        _context.AssertIsReadConnection();
    }

    public void AssertIsWriteConnection()
    {
        _context.AssertIsWriteConnection();
    }

    public void CloseAndDisposeConnection(DbConnection connection)
    {
        _context.CloseAndDisposeConnection(connection);
    }


    public DbMode ConnectionMode => DbMode.SingleConnection;

    public ITypeMapRegistry TypeMapRegistry => _context.TypeMapRegistry;

    public IDataSourceInformation DataSourceInfo => _context.DataSourceInfo;

    public string MissingSqlSettings => _context.MissingSqlSettings;

    public void Commit()
    {
        try
        {
            _transaction.Commit();
            _committed = true;
        }
        finally
        {
            _isCompleted = true;
           _context.CloseAndDisposeConnection(_connection);
        }
    }

    public void Rollback()
    {
        try
        {
            _transaction.Rollback();
            _rolledBack = true;
        }
        finally
        {
            _isCompleted = true;
            _context.CloseAndDisposeConnection(_connection);
        }
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void EnsureConnectionIsOpen()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
    }


    private void ThrowIfDisposed()
    {
        if (Interlocked.Read(ref _disposed) == 1)
            throw new ObjectDisposedException(nameof(TransactionContext));
    }

    private void Dispose(bool disposing)
    {
        if (Interlocked.Increment(ref _disposed) == 1)
        {
            if (!_committed) Rollback();
            if (_context.ConnectionMode == DbMode.Standard) _connection.Dispose();

            if (disposing) GC.SuppressFinalize(this);
        }
    }
}