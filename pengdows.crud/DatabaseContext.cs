using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text;

namespace pengdows.crud;

public class DatabaseContext : IDatabaseContext
{
    private int _connectionCount;
    private bool _isSqlServer;
    private string _missingSqlSettings;
    private DataSourceInformation _dataSourceInfo;
    private DbConnection? _connection;
    private readonly DbProviderFactory _factory;
    private readonly DbMode _connectionMode;
    private readonly ITypeMapRegistry _typeMapRegistry;

    private string ConnectionString { get; set; }

    public DbMode ConnectionMode => _connectionMode;

    public ITypeMapRegistry TypeMapRegistry => _typeMapRegistry;


    private DbConnection Connection => _connection ??
                                       throw new ObjectDisposedException(
                                           "attempt to use single connection from the wrong mode.");

    public IDataSourceInformation DataSourceInfo => _dataSourceInfo;


    public string MissingSqlSettings => (_missingSqlSettings ?? "");

    private void CheckForSqlServerSettings(DbConnection conn)
    {
        _isSqlServer =
            _dataSourceInfo.DatabaseProductName.StartsWith("Microsoft SQL Server", StringComparison.OrdinalIgnoreCase)
            && !_dataSourceInfo.DatabaseProductName.Contains("Compact", StringComparison.OrdinalIgnoreCase);

        if (!_isSqlServer) return;

        var settings = new Dictionary<string, string>
        {
            { "ANSI_NULLS", "ON" },
            { "ANSI_PADDING", "ON" },
            { "ANSI_WARNINGS", "ON" },
            { "ARITHABORT", "ON" },
            { "CONCAT_NULL_YIELDS_NULL", "ON" },
            { "QUOTED_IDENTIFIER", "ON" },
            { "NUMERIC_ROUNDABORT", "OFF" }
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name, setting FROM sys.configurations WHERE name IN (" +
                          string.Join(", ", settings.Keys.Select(s => $"'{s}'")) + ")";

        using var reader = cmd.ExecuteReader();
        var currentSettings = new Dictionary<string, string>();

        while (reader.Read())
        {
            currentSettings[reader.GetString(0)] = reader.GetString(1);
        }

        var sb = new StringBuilder();

        foreach (var (setting, expectedValue) in settings)
        {
            if (!currentSettings.TryGetValue(setting, out var currentValue) ||
                !currentValue.Equals(expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(setting);
                sb.Append(" ");
                sb.Append(expectedValue);
                sb.AppendLine(";");
                // _missingSqlSettings.AddOrUpdate(setting, expectedValue, (s, s1) => expectedValue);
            }
        }

        if (sb.Length > 0)
        {
            sb.Insert(0, "SET NOCOUNT ON;\n");
            sb.AppendLine("SET NOCOUNT OFF;");
            _missingSqlSettings = sb.ToString();
        }
    }

    // private void ApplyIndexedViewSettings(DbConnection conn)
    // {
    //     if (_missingSqlSettings.Count == 0 || !_isSqlServer) return;
    //
    //     using var cmd = conn.CreateCommand();
    //     var setCommands = _missingSqlSettings.Select(kvp => $"SET {kvp.Key} {kvp.Value}");
    //     cmd.CommandText = string.Join("; ", setCommands);
    //     cmd.ExecuteNonQuery();
    // }
    public DatabaseContext(string connectionString, string providerName, ITypeMapRegistry typeMapRegistry,
        DbMode mode = DbMode.Standard)
    {
        _typeMapRegistry = typeMapRegistry;
        _connectionMode = mode;
        _factory = DbProviderFactories.GetFactory(providerName);
        InitializeInternals(connectionString, mode);
    }

    private void InitializeInternals(string connectionString, DbMode mode)
    {
        DbConnection conn = null;
        try
        {
            var csb = _factory.CreateConnectionStringBuilder() ?? new DbConnectionStringBuilder();
            conn = _factory.CreateConnection();
            csb.ConnectionString = connectionString;
            conn.ConnectionString = csb.ConnectionString;
            AddStateChangeHandler(conn);
            conn.Open();
            _dataSourceInfo = new DataSourceInformation(conn);
            CheckForSqlServerSettings(conn);
            ConnectionString = csb.ConnectionString;
            if (mode != DbMode.Standard)
            {
                //ApplyIndexedViewSettings(conn);

                _connection = conn;
            }
        }
        finally
        {
            if (mode != DbMode.Standard)
            {
                conn?.Dispose();
            }
        }
    }

    public string WrapObjectName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        var ss = name.Split(_dataSourceInfo.SchemaSeparator);
        var qp = _dataSourceInfo.QuotePrefix;
        var qs = _dataSourceInfo.QuoteSuffix;
        if (name.Contains(qp) || name.Contains(qs))
        {
            return name; //already wrapped or contains a quote
        }

        var sb = new StringBuilder();
        foreach (var s in ss)
        {
            if (sb.Length > 0)
            {
                sb.Append(_dataSourceInfo.SchemaSeparator);
            }

            sb.Append(qp);
            sb.Append(s);
            sb.Append(qs);
        }

        return sb.ToString();
    }

    public TransactionContext BeginTransaction()
    {
        return new TransactionContext(this);
    }


    private DbConnection GetStandardConnection()
    {
        var conn = _factory.CreateConnection();
        conn.ConnectionString = ConnectionString;
        AddStateChangeHandler(conn);
        return conn;
    }

    private void AddStateChangeHandler(DbConnection connection)
    {
        connection.StateChange += (sender, args) =>
        {
            switch (args.CurrentState)
            {
                case ConnectionState.Open:
                    Interlocked.Increment(ref _connectionCount);
                    break;
                case ConnectionState.Closed:
                    Interlocked.Decrement(ref _connectionCount);
                    break;
            }
        };
    }

    private DbConnection GetSingleConnection(ExecutionType type)
    {
        return Connection;
    }

    private DbConnection GetSqlCeConnection(ExecutionType type)
    {
        if (ExecutionType.Read == type)
        {
            return GetStandardConnection();
        }

        return Connection;
    }

    private DbConnection GetSqlExpressUserModeConnection(ExecutionType type)
    {
        //Just return a new connection, leaving the 1 connection open;
        return GetStandardConnection();
    }

    public ISqlContainer CreateSqlContainer(string? query = null)
    {
        return new SqlContainer(this, TypeMapRegistry, query);
    }

    public DbParameter CreateDbParameter<T>(string name, DbType type, T value)
    {
        var p = _factory.CreateParameter() ?? throw new InvalidOperationException("Failed to create parameter.");
        p.ParameterName = name;
        p.DbType = type;
        p.Value = value;
        return p;
    }

    public DbConnection GetConnection(ExecutionType type = ExecutionType.Read)
    {
        switch (ConnectionMode)
        {
            case DbMode.Standard:
                return GetStandardConnection();
            case DbMode.SqlExpressUserMode:
                return GetSqlExpressUserModeConnection(ExecutionType.Write);
            case DbMode.SqlCe:
                return GetSqlCeConnection(ExecutionType.Write);
            case DbMode.SingleConnection:
                return GetSingleConnection(ExecutionType.Write);
            default:
                throw new InvalidOperationException("Invalid connection mode.");
        }
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Dispose();
        }
    }

    ~DatabaseContext()
    {
        Dispose(false);
    }
}