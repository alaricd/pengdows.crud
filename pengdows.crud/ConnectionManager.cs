#region
using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using pengdows.crud.enums;
using pengdows.crud.wrappers;
#endregion

namespace pengdows.crud;

internal class ConnectionManager
{
    private readonly DbProviderFactory _factory;
    private readonly ILogger<IDatabaseContext> _logger;
    private readonly string _connectionString;
    private readonly SessionSettingsManager _sessionSettings;
    private readonly bool _isReadConnection;
    private readonly bool _isWriteConnection;
    private readonly DbMode _mode;
    private readonly DataSourceInformation _info;
    private ITrackedConnection? _connection;
    private long _connectionCount;
    private long _maxNumberOfOpenConnections;

    public ConnectionManager(DbProviderFactory factory,
        ILogger<IDatabaseContext> logger,
        string connectionString,
        DataSourceInformation info,
        DbMode mode,
        bool isReadConnection,
        bool isWriteConnection,
        SessionSettingsManager sessionSettings,
        ITrackedConnection? connection = null)
    {
        _factory = factory;
        _logger = logger;
        _connectionString = connectionString;
        _info = info;
        _mode = mode;
        _isReadConnection = isReadConnection;
        _isWriteConnection = isWriteConnection;
        _sessionSettings = sessionSettings;
        _connection = connection;
    }

    public ITrackedConnection GetConnection(ExecutionType executionType, bool isShared = false)
    {
        return _mode switch
        {
            DbMode.Standard or DbMode.KeepAlive => GetStandardConnection(isShared),
            DbMode.SingleWriter => GetSingleWriterConnection(executionType),
            DbMode.SingleConnection => GetSingleConnection(),
            _ => throw new InvalidOperationException("Invalid connection mode.")
        };
    }

    public void AssertIsReadConnection()
    {
        if (!_isReadConnection)
            throw new InvalidOperationException("The connection is not read connection.");
    }

    public void AssertIsWriteConnection()
    {
        if (!_isWriteConnection)
            throw new InvalidOperationException("The connection is not write connection.");
    }

    public void CloseAndDisposeConnection(ITrackedConnection? connection)
    {
        if (connection == null) return;

        _logger.LogInformation($"Connection mode is: {_mode}");
        switch (_mode)
        {
            case DbMode.SingleConnection:
            case DbMode.SingleWriter:
            case DbMode.KeepAlive:
                if (_connection != connection)
                {
                    _logger.LogInformation("Not our single connection, closing");
                    connection.Dispose();
                }
                break;
            case DbMode.Standard:
                _logger.LogInformation("Closing a standard connection");
                try
                {
                    connection.Dispose();
                }
                catch
                {
                    _logger.LogInformation("Connection closed");
                }
                break;
            default:
                throw new NotSupportedException("Unsupported connection mode.");
        }
    }

    public async ValueTask CloseAndDisposeConnectionAsync(ITrackedConnection? connection)
    {
        if (connection == null)
            return;

        _logger.LogInformation($"Async Closing Connection in mode: {_mode}");

        if (connection is IAsyncDisposable asyncConnection)
            await asyncConnection.DisposeAsync().ConfigureAwait(false);
        else
            connection.Dispose();
    }

    private ITrackedConnection FactoryCreateConnection(string? connectionString = null, bool isSharedConnection = false)
    {
        var connection = _factory.CreateConnection();
        connection.ConnectionString = string.IsNullOrWhiteSpace(connectionString) ? _connectionString : connectionString;

        var tracked = new TrackedConnection(
            connection,
            (sender, args) =>
            {
                var to = args.CurrentState;
                var from = args.OriginalState;
                switch (to)
                {
                    case ConnectionState.Open:
                    {
                        _logger.LogDebug("Opening connection: " + _info.DatabaseProductName);
                        var now = Interlocked.Increment(ref _connectionCount);
                        UpdateMaxConnectionCount(now);
                        break;
                    }
                    case ConnectionState.Closed:
                    case ConnectionState.Broken:
                        _logger.LogDebug("Closed or broken connection: " + _info.DatabaseProductName);
                        Interlocked.Decrement(ref _connectionCount);
                        break;
                }
            },
            onFirstOpen: _sessionSettings.ApplyConnectionSessionSettings,
            onDispose: conn => { _logger.LogDebug("Connection disposed."); },
            null,
            isSharedConnection
        );
        return tracked;
    }

    private ITrackedConnection GetStandardConnection(bool isShared = false)
    {
        var conn = FactoryCreateConnection(null, isShared);
        return conn;
    }

    private ITrackedConnection GetSingleConnection()
    {
        return _connection ?? throw new ObjectDisposedException("attempt to use single connection from the wrong mode.");
    }

    private ITrackedConnection GetSingleWriterConnection(ExecutionType type, bool isShared = false)
    {
        if (ExecutionType.Read == type) return GetStandardConnection(isShared);

        return GetSingleConnection();
    }

    private void UpdateMaxConnectionCount(long current)
    {
        long previous;
        do
        {
            previous = Interlocked.Read(ref _maxNumberOfOpenConnections);
            if (current <= previous)
                return;
        } while (Interlocked.CompareExchange(
                     ref _maxNumberOfOpenConnections,
                     current,
                     previous) != previous);
    }

    public long MaxNumberOfConnections => Interlocked.Read(ref _maxNumberOfOpenConnections);

    public long NumberOfOpenConnections => Interlocked.Read(ref _connectionCount);

    public string QuotePrefix => _info.QuotePrefix;
    public string QuoteSuffix => _info.QuoteSuffix;
}
