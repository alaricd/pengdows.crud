#region

using System.Data;
using System.Data.Common;
using pengdows.crud.enums;
using pengdows.crud.threading;
using pengdows.crud.wrappers;

#endregion

namespace pengdows.crud;

public class TransactionContext : ITransactionContext
{
    private readonly ITrackedConnection _connection;
    private readonly DatabaseContext _context;
    private readonly IDbTransaction _transaction;
    private bool _committed;
    private long _disposed;
    private bool _isCompleted;
    private bool _rolledBack;

    private readonly SemaphoreSlim _semaphoreSlim;
    public bool WasCommitted => _committed;
    public bool WasRolledBack => _rolledBack;
    public Guid TransactionId { get; } = Guid.NewGuid();

    internal TransactionContext(IDatabaseContext context, IsolationLevel isolationLevel = IsolationLevel.Unspecified)
    {
        _context = context as DatabaseContext ?? throw new ArgumentNullException(nameof(context));
        if (_context.Product == SupportedDatabase.CockroachDb)
        {
            // this is the only level supported by cockroachdb
            isolationLevel = IsolationLevel.Serializable;
        }

        var executionType = GetExecutionAndSetIsolationTypes(ref isolationLevel);
        IsolationLevel = isolationLevel;
        _connection = _context.GetConnection(executionType, true);
        var locker = _connection.GetLock();
        Console.WriteLine("--------Connection locker type for Transaction=" + locker.GetType());
        EnsureConnectionIsOpen();
        _semaphoreSlim = new SemaphoreSlim(1, 1);


        _transaction = _connection.BeginTransaction(isolationLevel);
    }

    private ExecutionType GetExecutionAndSetIsolationTypes(ref IsolationLevel isolationLevel)
    {
        var executionType = ExecutionType.Write;
        switch (_context.ReadWriteMode)
        {
            case ReadWriteMode.ReadWrite:
            case ReadWriteMode.WriteOnly:
                //leave the default "write" selection
                if (isolationLevel < IsolationLevel.ReadCommitted)
                {
                    isolationLevel = IsolationLevel.ReadCommitted;
                }

                break;
            case ReadWriteMode.ReadOnly:
                executionType = ExecutionType.Read;
                if (isolationLevel < IsolationLevel.RepeatableRead)
                {
                    isolationLevel = IsolationLevel.RepeatableRead;
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return executionType;
    }

    public bool IsCompleted
    {
        get
        {
            _isCompleted |= _committed || _rolledBack;
            return _isCompleted;
        }
    }

    public IsolationLevel IsolationLevel { get; }


    public long NumberOfOpenConnections => _context.NumberOfOpenConnections;

    public string QuotePrefix => _context.QuotePrefix;

    public string QuoteSuffix => _context.QuoteSuffix;

    public string CompositeIdentifierSeparator => _context.CompositeIdentifierSeparator;
    public SupportedDatabase Product => _context.Product;

    public long MaxNumberOfConnections => _context.MaxNumberOfConnections;

    public bool IsReadOnlyConnection => _context.IsReadOnlyConnection;

    public ILockerAsync GetLock()
    {
        return new RealAsyncLocker(_semaphoreSlim);
    }

    public ISqlContainer CreateSqlContainer(string? query = null)
    {
        if (IsCompleted)
        {
            throw new Exception("Cannot create a sql container because this transaction is already completed.");
        }

        return new SqlContainer(this, query);
    }

    public DbParameter CreateDbParameter<T>(string name, DbType type, T value)
    {
        return _context.CreateDbParameter(name, type, value);
    }

    public ITrackedConnection GetConnection(ExecutionType type, bool isShared = false)
    {
        return _connection;
    }

    public string WrapObjectName(string name)
    {
        return _context.WrapObjectName(name);
    }

    public TransactionContext BeginTransaction(IsolationLevel? isolationLevel = null)
    {
        throw new InvalidOperationException("Cannot begin a transaction without an open connection.");
    }

    public string GenerateRandomName(int length = 5, int parameterNameMaxLength = 30)
    {
        return _context.GenerateRandomName(length, parameterNameMaxLength);
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

    public string MakeParameterName(string parameterName)
    {
        return _context.MakeParameterName(parameterName);
    }

    public void CloseAndDisposeConnection(ITrackedConnection? connection)
    {
        //  _context.CloseAndDisposeConnection(connection);
    }

    public string MakeParameterName(DbParameter dbParameter)
    {
        return _context.MakeParameterName(dbParameter);
    }

    public ProcWrappingStyle ProcWrappingStyle
    {
        get => _context.DataSourceInfo.ProcWrappingStyle;
        set => throw new NotImplementedException();
    }


    public int MaxParameterLimit => _context.DataSourceInfo.MaxParameterLimit;

    public DbMode ConnectionMode => DbMode.SingleConnection;

    public ITypeMapRegistry TypeMapRegistry => _context.TypeMapRegistry;

    public IDataSourceInformation DataSourceInfo => _context.DataSourceInfo;

    public string SessionSettingsPreamble => _context.SessionSettingsPreamble;

    internal IDbTransaction Transaction => _transaction;

    public void Commit()
    {
        if (IsCompleted)
        {
            throw new InvalidOperationException("Transaction already completed.");
        }

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
        if (IsCompleted)
        {
            throw new InvalidOperationException("Transaction already completed.");
        }

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
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return; // Already disposed

        if (!_committed && !_rolledBack)
        {
            try
            {
                _transaction.Rollback();
                _rolledBack = true;
            }
            catch
            {
                // Log or handle rollback failure if needed
            }
        }

        if (disposing)
        {
            _transaction.Dispose();
            _context.CloseAndDisposeConnection(_connection);
            _semaphoreSlim.Dispose();
        }

        _isCompleted = true;
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return; // Already disposed

        if (!_committed && !_rolledBack)
        {
            try
            {
                _transaction.Rollback();
                _rolledBack = true;
            }
            catch
            {
                // Log or handle rollback failure asynchronously if required
            }
        }

        if (_transaction is IAsyncDisposable asyncTransaction)
        {
            await asyncTransaction.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _transaction.Dispose();
        }

        if (_connection is IAsyncDisposable asyncConnection)
        {
            await asyncConnection.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _context.CloseAndDisposeConnection(_connection);
        }

        _semaphoreSlim.Dispose();
        _isCompleted = true;
    }

    ~TransactionContext()
    {
        Dispose(false);
    }

    private void ThrowIfDisposed()
    {
        if (Interlocked.Read(ref _disposed) == 1)
            throw new ObjectDisposedException(nameof(TransactionContext));
    }

    private void EnsureConnectionIsOpen()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
    }
    
}