using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace pengdows.crud.wrappers;

internal class TrackedConnection : ITrackedConnection, IAsyncDisposable
{
    private readonly DbConnection _connection;
    private readonly Action<ConnectionState, ConnectionState>? _onStateChange;
    private readonly Action<DbConnection>? _onFirstOpen;
    private readonly Action<DbConnection>? _onDispose;
    private bool _wasOpened;
    private readonly string _name;
    private ILogger<TrackedConnection> _logger;

    internal TrackedConnection(
        DbConnection conn,
        Action<ConnectionState, ConnectionState>? onStateChange = null,
        Action<DbConnection>? onFirstOpen = null,
        Action<DbConnection>? onDispose = null)
    {
        _connection = conn ?? throw new ArgumentNullException(nameof(conn));
        _onStateChange = onStateChange;
        _onFirstOpen = onFirstOpen;
        _onDispose = onDispose;
        _name = Guid.NewGuid().ToString();
        _logger = new NullLogger<TrackedConnection>();
    }

    private void TriggerFirstOpen()
    {
        if (!_wasOpened)
        {
            _wasOpened = true;
            _onFirstOpen?.Invoke(_connection);
        }
    }

    public IDbTransaction BeginTransaction()
        => _connection.BeginTransaction();

    public IDbTransaction BeginTransaction(IsolationLevel isolationLevel)
        => _connection.BeginTransaction(isolationLevel);

    public void ChangeDatabase(string databaseName)
        => throw new NotImplementedException("This method is not allowed.");

    public void Close()
        => _connection.Close();

    public IDbCommand CreateCommand()
        => _connection.CreateCommand();

    public void Open()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _connection.Open();
        }
        finally
        {
            stopwatch.Stop();
            _logger?.LogInformation("Connection opened in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
        }

        TriggerFirstOpen();
    }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            _logger?.LogInformation("Connection opened in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
        }

        TriggerFirstOpen();
    }

    [AllowNull]
    public string ConnectionString
    {
        get => _connection.ConnectionString;
        set => _connection.ConnectionString = value;
    }

    public int ConnectionTimeout => _connection.ConnectionTimeout;
    public string Database => _connection.Database;
    public ConnectionState State => _connection.State;
    public string DataSource => _connection.DataSource;
    public string ServerVersion => _connection.ServerVersion;

    public DataTable GetSchema(string dataSourceInformation)
        => _connection.GetSchema(dataSourceInformation);

    public void Dispose()
    {
        _onDispose?.Invoke(_connection);
        _connection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _onDispose?.Invoke(_connection);
        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}