#region

using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using pengdows.crud.enums;
using pengdows.crud.infrastructure;
using pengdows.crud.threading;
using pengdows.crud.wrappers;

#endregion

namespace pengdows.crud;

public class TransactionContext : SafeAsyncDisposableBase, ITransactionContext
{
    private readonly ITrackedConnection _connection;
    private readonly DatabaseContext _context;
    private readonly IDbTransaction _transaction;

    private readonly SemaphoreSlim _semaphoreSlim;
    private readonly ILogger<TransactionContext> _logger;

    private int _completedState; // 0 = not completed, 1 = committed or rolled back
    private int _semaphoreDisposed;
    private long _disposed;

    public bool WasCommitted => _completedState == 1 && _committed;
    public bool WasRolledBack => _completedState == 1 && _rolledBack;

    private bool _committed;
    private bool _rolledBack;

    public Guid TransactionId { get; } = Guid.NewGuid();

    internal TransactionContext(IDatabaseContext context,
        IsolationLevel isolationLevel = IsolationLevel.Unspecified,
        ILogger<TransactionContext>? logger = null)
    {
        _logger = logger ?? new NullLogger<TransactionContext>();
        _context = context as DatabaseContext ?? throw new ArgumentNullException(nameof(context));

        if (_context.Product == SupportedDatabase.CockroachDb)
        {
            isolationLevel = IsolationLevel.Serializable;
        }

        var executionType = GetExecutionAndSetIsolationTypes(ref isolationLevel);
        IsolationLevel = isolationLevel;

        _connection = _context.GetConnection(executionType, true);
        EnsureConnectionIsOpen();
        _semaphoreSlim = new SemaphoreSlim(1, 1);

        _transaction = _connection.BeginTransaction(isolationLevel);
    }

    public bool IsCompleted => Interlocked.CompareExchange(ref _completedState, 0, 0) != 0;

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

    public IsolationLevel IsolationLevel { get; }

    // Delegated context properties
    public long NumberOfOpenConnections => _context.NumberOfOpenConnections;
    public string QuotePrefix => _context.QuotePrefix;
    public string QuoteSuffix => _context.QuoteSuffix;
    public string CompositeIdentifierSeparator => _context.CompositeIdentifierSeparator;
    public SupportedDatabase Product => _context.Product;
    public long MaxNumberOfConnections => _context.MaxNumberOfConnections;
    public bool IsReadOnlyConnection => _context.IsReadOnlyConnection;
    public int MaxParameterLimit => _context.MaxParameterLimit;
    public DbMode ConnectionMode => DbMode.SingleConnection;
    public ITypeMapRegistry TypeMapRegistry => _context.TypeMapRegistry;
    public IDataSourceInformation DataSourceInfo => _context.DataSourceInfo;
    public string SessionSettingsPreamble => _context.SessionSettingsPreamble;

    internal IDbTransaction Transaction => _transaction;

    public ILockerAsync GetLock() => new RealAsyncLocker(_semaphoreSlim);

    public ISqlContainer CreateSqlContainer(string? query = null)
    {
        if (IsCompleted)
            throw new InvalidOperationException("Cannot create a SQL container because the transaction is completed.");

        return new SqlContainer(this, query);
    }

    public DbParameter CreateDbParameter<T>(string name, DbType type, T value) =>
        _context.CreateDbParameter(name, type, value);

    public DbParameter CreateDbParameter<T>(DbType type, T value) => _context.CreateDbParameter(type, value);
    public ITrackedConnection GetConnection(ExecutionType type, bool isShared = false) => _connection;
    public string WrapObjectName(string name) => _context.WrapObjectName(name);

    public string GenerateRandomName(int length = 5, int parameterNameMaxLength = 30) =>
        _context.GenerateRandomName(length, parameterNameMaxLength);

    public void AssertIsReadConnection() => _context.AssertIsReadConnection();
    public void AssertIsWriteConnection() => _context.AssertIsWriteConnection();
    public string MakeParameterName(string parameterName) => _context.MakeParameterName(parameterName);

    public void CloseAndDisposeConnection(ITrackedConnection? conn)
    {
        //throw new NotImplementedException();
    }

    public string MakeParameterName(DbParameter dbParameter) => _context.MakeParameterName(dbParameter);

    public ProcWrappingStyle ProcWrappingStyle
    {
        get => _context.ProcWrappingStyle;
        set => throw new NotImplementedException();
    }

    public ITransactionContext BeginTransaction(IsolationLevel? isolationLevel = null)
        => throw new InvalidOperationException("Cannot begin a nested transaction from TransactionContext.");

    public void Commit()
    {
        ThrowIfDisposed();
        _semaphoreSlim.Wait();

        try
        {
            if (Interlocked.Exchange(ref _completedState, 1) != 0)
                throw new InvalidOperationException("Transaction already completed.");

            _transaction.Commit();
            _committed = true;
        }
        finally
        {
            Interlocked.Exchange(ref _completedState, 1);
            _context.CloseAndDisposeConnection(_connection);
            _semaphoreSlim.Release();
        }
    }

    public void Rollback()
    {
        ThrowIfDisposed();
        _semaphoreSlim.Wait();

        try
        {
            if (Interlocked.Exchange(ref _completedState, 1) != 0)
                throw new InvalidOperationException("Transaction already completed.");

            _transaction.Rollback();
            _rolledBack = true;
        }
        finally
        {
            Interlocked.Exchange(ref _completedState, 1);
            _context.CloseAndDisposeConnection(_connection);
            _semaphoreSlim.Release();
        }
    }

    protected override void DisposeManaged()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (Interlocked.CompareExchange(ref _completedState, 0, 0) == 0)
        {
            try
            {
                _semaphoreSlim.Wait();

                try
                {
                    if (Interlocked.Exchange(ref _completedState, 1) != 0)
                        throw new InvalidOperationException("Transaction already completed.");

                    _transaction.Rollback();
                    _rolledBack = true;
                }
                finally
                {
                    Interlocked.Exchange(ref _completedState, 1);
                    _context.CloseAndDisposeConnection(_connection);
                    _semaphoreSlim.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rollback failed during Dispose.");
            }
        }

        if (Interlocked.Exchange(ref _semaphoreDisposed, 1) == 0)
        {
            _semaphoreSlim.Dispose();
        }
    }

    protected override async ValueTask DisposeManagedAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (Interlocked.CompareExchange(ref _completedState, 0, 0) == 0)
        {
            try
            {
                _semaphoreSlim.Wait();

                try
                {
                    if (Interlocked.Exchange(ref _completedState, 1) != 0)
                        throw new InvalidOperationException("Transaction already completed.");

                    _transaction.Rollback();
                    _rolledBack = true;
                }
                finally
                {
                    Interlocked.Exchange(ref _completedState, 1);
                    _context.CloseAndDisposeConnection(_connection);
                    _semaphoreSlim.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Async rollback failed during DisposeAsync.");
            }
        }

        if (_transaction is IAsyncDisposable asyncTx)
        {
            await asyncTx.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _transaction.Dispose();
        }

        await _context.CloseAndDisposeConnectionAsync(_connection).ConfigureAwait(false);

        if (Interlocked.Exchange(ref _semaphoreDisposed, 1) == 0)
        {
            _semaphoreSlim.Dispose();
        }
    }

    private void EnsureConnectionIsOpen()
    {
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }
    }

    private void ThrowIfDisposed()
    {
        if (Interlocked.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(TransactionContext));
    }
}