#region
using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using pengdows.crud.enums;
using pengdows.crud.wrappers;
#endregion

namespace pengdows.crud;

public partial class DatabaseContext
{
    public ITrackedConnection GetConnection(ExecutionType executionType, bool isShared = false)
    {
        switch (ConnectionMode)
        {
            case DbMode.Standard:
            case DbMode.KeepAlive:
                return GetStandardConnection(isShared);
            case DbMode.SingleWriter:
                return GetSingleWriterConnection(executionType);
            case DbMode.SingleConnection:
                return GetSingleConnection();
            default:
                throw new InvalidOperationException("Invalid connection mode.");
        }
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

        _logger.LogInformation($"Connection mode is: {ConnectionMode}");
        switch (ConnectionMode)
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

        _logger.LogInformation($"Async Closing Connection in mode: {ConnectionMode}");

        if (connection is IAsyncDisposable asyncConnection)
            await asyncConnection.DisposeAsync().ConfigureAwait(false);
        else
            connection.Dispose();
    }

    private ITrackedConnection FactoryCreateConnection(string? connectionString = null, bool isSharedConnection = false)
    {
        SanitizeConnectionString(connectionString);

        var connection = _factory.CreateConnection();
        connection.ConnectionString = ConnectionString;

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
                        _logger.LogDebug("Opening connection: " + Name);
                        var now = Interlocked.Increment(ref _connectionCount);
                        UpdateMaxConnectionCount(now);
                        break;
                    }
                    case ConnectionState.Closed:
                    case ConnectionState.Broken:
                        _logger.LogDebug("Closed or broken connection: " + Name);
                        Interlocked.Decrement(ref _connectionCount);
                        break;
                }
            },
            onFirstOpen: ApplyConnectionSessionSettings,
            onDispose: conn => { _logger.LogDebug("Connection disposed."); },
            null,
            isSharedConnection
        );
        return tracked;
    }

    private void SanitizeConnectionString(string? connectionString)
    {
        if (connectionString != null && string.IsNullOrWhiteSpace(ConnectionString))
        {
            var csb = GetFactoryConnectionStringBuilder(connectionString);
            var tmp = csb.ConnectionString;
            ConnectionString = tmp;
        }
    }

    private ITrackedConnection GetStandardConnection(bool isShared = false)
    {
        var conn = FactoryCreateConnection(null, isShared);
        return conn;
    }

    private ITrackedConnection GetSingleConnection()
    {
        return Connection;
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

    public string QuotePrefix => DataSourceInfo.QuotePrefix;
    public string QuoteSuffix => DataSourceInfo.QuoteSuffix;
}
