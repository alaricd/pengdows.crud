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
    private ITrackedConnection? _connection = null;

    private long _connectionCount;
    private string _connectionString;
    private DataSourceInformation _dataSourceInfo;
    private IIsolationResolver _isolationResolver;
    private bool _isReadConnection = true;
    private bool _isSqlServer;
    private bool _isWriteConnection = true;
    private long _maxNumberOfOpenConnections;

    private ParameterFactory _parameterFactory = null!;
    private SessionSettingsManager _sessionSettingsManager = null!;
    private ConnectionManager _connectionManager = null!;

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


    public string SessionSettingsPreamble => _sessionSettingsManager.SessionSettingsPreamble;

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

    public DbParameter CreateDbParameter<T>(string? name, DbType type, T value)
    {
        return _parameterFactory.CreateDbParameter(name, type, value);
    }

    public DbParameter CreateDbParameter<T>(DbType type, T value)
    {
        return _parameterFactory.CreateDbParameter(type, value);
    }

    public string GenerateRandomName(int length = 5, int parameterNameMaxLength = 30)
    {
        return _parameterFactory.GenerateRandomName(length, parameterNameMaxLength);
    }

    public string MakeParameterName(DbParameter dbParameter)
    {
        return _parameterFactory.MakeParameterName(dbParameter);
    }

    public string MakeParameterName(string parameterName)
    {
        return _parameterFactory.MakeParameterName(parameterName);
    }

    public ITrackedConnection GetConnection(ExecutionType executionType, bool isShared = false)
    {
        return _connectionManager.GetConnection(executionType, isShared);
    }

    public void CloseAndDisposeConnection(ITrackedConnection? connection)
    {
        _connectionManager.CloseAndDisposeConnection(connection);
    }

    public async ValueTask CloseAndDisposeConnectionAsync(ITrackedConnection? connection)
    {
        await _connectionManager.CloseAndDisposeConnectionAsync(connection).ConfigureAwait(false);
    }

    public void AssertIsReadConnection()
    {
        _connectionManager.AssertIsReadConnection();
    }

    public void AssertIsWriteConnection()
    {
        _connectionManager.AssertIsWriteConnection();
    }

    public ProcWrappingStyle ProcWrappingStyle
    {
        get => _parameterFactory.ProcWrappingStyle;
        set => _parameterFactory.ProcWrappingStyle = value;
    }

    public int MaxParameterLimit => _parameterFactory.MaxParameterLimit;

    public long MaxNumberOfConnections => _connectionManager.MaxNumberOfConnections;

    public long NumberOfOpenConnections => _connectionManager.NumberOfOpenConnections;

    public string QuotePrefix => _connectionManager.QuotePrefix;

    public string QuoteSuffix => _connectionManager.QuoteSuffix;

    private void InitializeInternals(IDatabaseContextConfiguration config)
    {
        var connectionString = config.ConnectionString;
        var mode = config.DbMode;
        var readWriteMode = config.ReadWriteMode;
        ITrackedConnection? conn = null;
        try
        {
            _isReadConnection = (readWriteMode & ReadWriteMode.ReadOnly) == ReadWriteMode.ReadOnly;
            _isWriteConnection = (readWriteMode & ReadWriteMode.WriteOnly) == ReadWriteMode.WriteOnly;

            var rawConn = _factory.CreateConnection();
            rawConn.ConnectionString = connectionString;
            conn = new TrackedConnection(rawConn);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                throw new ConnectionFailedException(ex.Message);
            }

            _sessionSettingsManager = new SessionSettingsManager(_logger, _factory, connectionString);
            _dataSourceInfo = DataSourceInformation.Create(conn);
            _sessionSettingsManager.SetupConnectionSessionSettingsForProvider(conn, _dataSourceInfo);
            Name = _dataSourceInfo.DatabaseProductName;
            if (_dataSourceInfo.Product == SupportedDatabase.Sqlite)
            {
                var csb = _sessionSettingsManager.GetFactoryConnectionStringBuilder(string.Empty);
                var ds = csb["Data Source"] as string;
                ConnectionMode = ":memory:" == ds ? DbMode.SingleConnection : DbMode.SingleWriter;
                mode = ConnectionMode;
            }

            if (mode != DbMode.Standard)
            {
                _sessionSettingsManager.ApplyConnectionSessionSettings(conn);
                _connection = conn;
            }

            _parameterFactory = new ParameterFactory(_factory, _dataSourceInfo);
            _connectionManager = new ConnectionManager(
                _factory,
                _logger,
                connectionString,
                _dataSourceInfo,
                mode,
                _isReadConnection,
                _isWriteConnection,
                _sessionSettingsManager,
                _connection);
        }
        finally
        {
            _isolationResolver ??= new IsolationResolver(Product, RCSIEnabled);
            if (mode == DbMode.Standard)
                conn?.Dispose();
        }
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
