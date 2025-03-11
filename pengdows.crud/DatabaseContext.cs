using System.Data;
using System.Data.Common;
using System.Text;

namespace pengdows.crud;

public class DatabaseContext : IDatabaseContext
{
  private int _disposeCount;
    private int _connectionCount;
    private bool IsSqlServer;
    private readonly HashSet<string> MissingSqlSettings = new();
    private DataSourceInformation _dataSourceInfo;

    private string ConnectionString { get;  set; }
    public DbMode ConnectionMode { get; private set; }
    public ITypeMapRegistry TypeMapRegistry { get; }

   
    public DbConnection Connection { get; private set; }

    public IDataSourceInformation DataSourceInfo => _dataSourceInfo;

    private DbProviderFactory Factory { get; set; }



    private void CheckForSqlServerSettings(DbConnection conn)
    {
        IsSqlServer = _dataSourceInfo.DatabaseProductName.StartsWith("Microsoft SQL Server", StringComparison.OrdinalIgnoreCase)
                      && !_dataSourceInfo.DatabaseProductName.Contains("Compact", StringComparison.OrdinalIgnoreCase);

        if (!IsSqlServer) return;

        var settings = new Dictionary<string, string>
        {
            {"ANSI_NULLS", "ON"},
            {"ANSI_PADDING", "ON"},
            {"ANSI_WARNINGS", "ON"},
            {"ARITHABORT", "ON"},
            {"CONCAT_NULL_YIELDS_NULL", "ON"},
            {"QUOTED_IDENTIFIER", "ON"},
            {"NUMERIC_ROUNDABORT", "OFF"}
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

        foreach (var (setting, expectedValue) in settings)
        {
            if (!currentSettings.TryGetValue(setting, out var currentValue) || !currentValue.Equals(expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                MissingSqlSettings.Add(setting);
            }
        }
    }

    private void ApplyIndexedViewSettings(DbConnection conn)
    {
        if (MissingSqlSettings.Count == 0 || !IsSqlServer) return;

        using var cmd = conn.CreateCommand();
        var setCommands = MissingSqlSettings.Select(s => $"{s} {(s == "NUMERIC_ROUNDABORT" ? "OFF" : "ON")}");
        cmd.CommandText = string.Join("; ", setCommands);
        cmd.ExecuteNonQuery();
    }
    public DatabaseContext(string connectionString, string providerName, ITypeMapRegistry typeMapRegistry, DbMode mode = DbMode.Standard) 
    {
        TypeMapRegistry = typeMapRegistry;
        this.ConnectionMode = mode;
        this.Factory = DbProviderFactories.GetFactory(providerName);
        var csb = this. Factory.CreateConnectionStringBuilder() ?? new DbConnectionStringBuilder();
        DbConnection conn = null;
        try
        {
            csb.ConnectionString = connectionString;
            conn.ConnectionString = csb.ConnectionString;

            conn.Open();
            _dataSourceInfo = new DataSourceInformation(conn);
            CheckForSqlServerSettings(conn);
            ApplyIndexedViewSettings(conn);
            Interlocked.Increment(ref _connectionCount);
            ConnectionString = csb.ConnectionString;
            if (mode != DbMode.Standard)
            {
                Connection = conn;
            }
        }
        finally
        {
            if (mode != DbMode.Standard)
            {
                Interlocked.Decrement(ref _connectionCount);
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

    public  TransactionContext BeginTransaction()
    {
        return new TransactionContext(this);
    }


    private DbConnection GetStandardConnection()
    {
        var connection = Factory.CreateConnection();
        connection.ConnectionString = ConnectionString;
        AddStateChangeHandler(connection);
        return connection;
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
        var p =  Factory.CreateParameter()?? throw new InvalidOperationException("Failed to create parameter.");
        p.ParameterName = name;
        p.DbType = type;
        p.Value = value;
        return p;
    }

    public  DbConnection GetConnection(ExecutionType type = ExecutionType.Read)
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
        
    }
}