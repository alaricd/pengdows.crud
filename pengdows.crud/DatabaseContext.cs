#region

using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using pengdows.crud.configuration;
using pengdows.crud.enums;
using pengdows.crud.exceptions;
using pengdows.crud.infrastructure;
using pengdows.crud.isolation;
using pengdows.crud.threading;
using pengdows.crud.wrappers;

#endregion

namespace pengdows.crud;

public class DatabaseContext : SafeAsyncDisposableBase, IDatabaseContext
{
    private readonly DbProviderFactory _factory;
    private readonly ILogger<IDatabaseContext> _logger;
    private bool _applyConnectionSessionSettings;
    private ITrackedConnection? _connection = null;

    private long _connectionCount;
    private string _connectionSessionSettings;
    private string _connectionString;
    private DataSourceInformation _dataSourceInfo;
    private IIsolationResolver _isolationResolver;
    private bool _isReadConnection = true;
    private bool _isSqlServer;
    private bool _isWriteConnection = true;
    private long _maxNumberOfOpenConnections;

    [Obsolete("Use the constructor that takes DatabaseContextConfiguration instead.")]
    public DatabaseContext(
        string connectionString,
        string providerFactory,
        ITypeMapRegistry? typeMapRegistry = null,
        DbMode mode = DbMode.Standard,
        ReadWriteMode readWriteMode = ReadWriteMode.ReadWrite,
        ILoggerFactory? loggerFactory = null)
        : this(
            new DatabaseContextConfiguration
            {
                ProviderName = providerFactory,
                ConnectionString = connectionString,
                ReadWriteMode = readWriteMode,
                DbMode = mode
            },
            DbProviderFactories.GetFactory(providerFactory ?? throw new ArgumentNullException(nameof(providerFactory))),
            (loggerFactory ?? NullLoggerFactory.Instance))
    {
    }

    [Obsolete("Use the constructor that takes DatabaseContextConfiguration instead.")]
    public DatabaseContext(
        string connectionString,
        DbProviderFactory factory,
        ITypeMapRegistry? typeMapRegistry = null,
        DbMode mode = DbMode.Standard,
        ReadWriteMode readWriteMode = ReadWriteMode.ReadWrite,
        ILoggerFactory? loggerFactory = null)
        : this(
            new DatabaseContextConfiguration
            {
                ConnectionString = connectionString,
                ReadWriteMode = readWriteMode,
                DbMode = mode
            },
            factory,
            (loggerFactory ?? NullLoggerFactory.Instance))
    {
    }

    public DatabaseContext(
        IDatabaseContextConfiguration configuration,
        DbProviderFactory factory,
        ILoggerFactory? loggerFactory = null)
    {
        try
        {
            loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = loggerFactory?.CreateLogger<IDatabaseContext>() ?? NullLogger<IDatabaseContext>.Instance;
            ReadWriteMode = configuration.ReadWriteMode;
            TypeMapRegistry = new TypeMapRegistry();
            ConnectionMode = configuration.DbMode;
            _factory = factory ?? throw new NullReferenceException(nameof(factory));

            InitializeInternals(configuration);
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            throw;
        }
    }

    public ReadWriteMode ReadWriteMode { get; }

    public string Name { get; set; }

    private string ConnectionString
    {
        get => _connectionString;
        set
        {
            //don't let it change
            if (!string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Connection string reset attempted.");

            _connectionString = value;
        }
    }


    private ITrackedConnection Connection => _connection ??
                                             throw new ObjectDisposedException(
                                                 "attempt to use single connection from the wrong mode.");


    public bool IsReadOnlyConnection => _isReadConnection && !_isWriteConnection;
    public bool RCSIEnabled { get; }

    public ILockerAsync GetLock()
    {
        return NoOpAsyncLocker.Instance;
    }


    public DbMode ConnectionMode { get; private set; }

    public ITypeMapRegistry TypeMapRegistry { get; }

    public IDataSourceInformation DataSourceInfo => _dataSourceInfo;


    public string SessionSettingsPreamble => _connectionSessionSettings ?? "";

    public string WrapObjectName(string name)
    {
        var qp = QuotePrefix;
        var qs = QuoteSuffix;
        var tmp = name?.Replace(qp, string.Empty)?.Replace(qs, string.Empty);
        if (string.IsNullOrEmpty(tmp)) return string.Empty;

        var ss = tmp.Split(CompositeIdentifierSeparator);

        var sb = new StringBuilder();
        foreach (var s in ss)
        {
            if (sb.Length > 0) sb.Append(CompositeIdentifierSeparator);

            sb.Append(qp);
            sb.Append(s);
            sb.Append(qs);
        }

        return sb.ToString();
    }


    public ITransactionContext BeginTransaction(IsolationLevel? isolationLevel = null)
    {
        if (!_isWriteConnection && isolationLevel is null) isolationLevel = IsolationLevel.RepeatableRead;

        isolationLevel ??= IsolationLevel.ReadCommitted;

        if (!_isWriteConnection && isolationLevel != IsolationLevel.RepeatableRead)
            throw new InvalidOperationException("Read-only transactions must use 'RepeatableRead'.");

        return new TransactionContext(this, isolationLevel.Value);
    }

    public ITransactionContext BeginTransaction(IsolationProfile isolationProfile)
    {
        return new TransactionContext(this, _isolationResolver.Resolve(isolationProfile));
    }


    public string CompositeIdentifierSeparator => _dataSourceInfo.CompositeIdentifierSeparator;
    public SupportedDatabase Product => _dataSourceInfo.Product;

    public ISqlContainer CreateSqlContainer(string? query = null)
    {
        return new SqlContainer(this, query);
    }

    protected override void DisposeManaged()
    {
        _connection?.Dispose();
        _connection = null;
        base.DisposeManaged();
    }
}
