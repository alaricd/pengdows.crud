using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using pengdows.crud.exceptions;

namespace pengdows.crud;

public class DatabaseContext : IDatabaseContext
{
    private static readonly Random _random = new();
    private readonly DbProviderFactory _factory;
    private bool _isReadConnection = true;
    private bool _isWriteConnection = true;
    private DbConnection? _connection;

    private long _connectionCount;
    private DataSourceInformation _dataSourceInfo;
    private bool _isSqlServer;
    private string _connectionSessionSettings;
    private int _numberOfOpenConnections;
    private bool _applyConnectionSessionSettings;
    private readonly ILogger<IDatabaseContext> _logger;

    public string Name { get; set; }

    public DatabaseContext(string connectionString,
        DbProviderFactory factory,
        ITypeMapRegistry typeMapRegistry = null,
        DbMode mode = DbMode.Standard,
        ReadWriteMode readWriteMode = ReadWriteMode.ReadWrite,
        ILogger<IDatabaseContext> logger = null)
    {
        TypeMapRegistry = typeMapRegistry ?? new TypeMapRegistry();
        ConnectionMode = mode;
        _factory = factory;
        _logger = logger ?? NullLogger<IDatabaseContext>.Instance;
        InitializeInternals(connectionString, mode, readWriteMode);
    }

    private string ConnectionString { get; set; }


    private DbConnection Connection => _connection ??
                                       throw new ObjectDisposedException(
                                           "attempt to use single connection from the wrong mode.");


    public DbMode ConnectionMode { get; private set; }

    public ITypeMapRegistry TypeMapRegistry { get; }

    public IDataSourceInformation DataSourceInfo => _dataSourceInfo;


    public string SessionSettingsPreamble => _connectionSessionSettings ?? "";

    public string WrapObjectName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        var ss = name.Split(CompositeIdentifierSeparator);
        var qp = QuotePrefix;
        var qs = QuoteSuffix;
        if (name.Contains(qp) || name.Contains(qs)) return name; //already wrapped or contains a quote

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

    public TransactionContext BeginTransaction(IsolationLevel? isolationLevel = null)
    {
        if (!_isWriteConnection && isolationLevel is null)
        {
            isolationLevel = IsolationLevel.RepeatableRead;
        }

        isolationLevel ??= IsolationLevel.ReadCommitted;

        if (!_isWriteConnection && isolationLevel != IsolationLevel.RepeatableRead)
            throw new InvalidOperationException("Read-only transactions must use 'RepeatableRead'.");

        return new TransactionContext(this, isolationLevel.Value);
    }


    public string CompositeIdentifierSeparator => _dataSourceInfo.CompositeIdentifierSeparator;

    public ISqlContainer CreateSqlContainer(string? query = null)
    {
        return new SqlContainer(this, query);
    }

    public DbParameter CreateDbParameter<T>(string name, DbType type, T value)
    {
        var p = _factory.CreateParameter() ?? throw new InvalidOperationException("Failed to create parameter.");

        if (string.IsNullOrEmpty(name)) name = GenerateRandomName();

        var valueIsNull = Utils.IsNullOrDbNull(value);
        p.ParameterName = name;
        p.DbType = type;
        p.Value = valueIsNull ? DBNull.Value : value;
        if (!valueIsNull && p.DbType == DbType.String && value is string s)
        {
            p.Size = Math.Max(s.Length, 1);
        }

        return p;
    }


    public DbConnection GetConnection(ExecutionType executionType = ExecutionType.Read)
    {
        switch (ConnectionMode)
        {
            case DbMode.Standard:
                return GetStandardConnection();
            case DbMode.SqlExpressUserMode:
                return GetSqlExpressUserModeConnection();
            case DbMode.SqlCe:
                return GetSqlCeConnection(executionType);
            case DbMode.SingleConnection:
                return GetSingleConnection();
            default:
                throw new InvalidOperationException("Invalid connection mode.");
        }
    }

    public void Dispose()
    {
        Dispose(true);
    }

    public string GenerateRandomName(int length = 8)
    {
        var validchars = "abcdefghijklmnopqrstuvwuxyzABCDEFGHIJKLMNOPQRSTUVWXYZ01234567890_".ToCharArray();

        // If the desired length exceeds the max allowed length, adjust to max length
        var dsi = _dataSourceInfo;
        var len = Math.Max(length, 2);
        len = Math.Min(len, dsi.ParameterNameMaxLength);
        // Create a buffer to store random bytes
        var buffer = new byte[len];

        // StringBuilder to construct the final random name
        var ca = new StringBuilder();

        // Generate random bytes and append corresponding lowercase letters to StringBuilder
        _random.NextBytes(buffer);
        var i = 0;
        var x = validchars.Length;
        foreach (var b in buffer)
        {
            // Convert each byte to a letter in validchars array
            // enforcing the fist letter into the 'a' to 'z' range
            var mod = x;
            if (i++ == 0) mod = 52;

            ca.Append(validchars[b % mod]);
        }

        // Get the generated name as a string
        var generatedName = ca.ToString();

        // Validate the generated name using the ParameterNamePattern regex
        if (!dsi.ParameterNamePatternRegex.IsMatch(generatedName))
        {
            _logger.LogInformation(generatedName);
            // If the name is not valid, regenerate a new one
            return GenerateRandomName(len);
        }

        return generatedName;
    }

    public DbParameter CreateDbParameter<T>(DbType type, T value)
    {
        return CreateDbParameter(null, type, value);
    }

    public void AssertIsReadConnection()
    {
        if (!_isReadConnection)
        {
            throw new InvalidOperationException("The connection is not read connection.");
        }
    }

    public void AssertIsWriteConnection()
    {
        if (!_isWriteConnection)
        {
            throw new InvalidOperationException("The connection is not write connection.");
        }
    }

    public void CloseAndDisposeConnection(DbConnection connection)
    {
        if (connection == null)
        {
            return;
        }

        _logger.LogInformation($"Connection mode is: {ConnectionMode}");
        switch (ConnectionMode)
        {
            case DbMode.SingleConnection:
            case DbMode.SqlCe:
                if (_connection != connection)
                {
                    //never close our single write connection
                    _logger.LogInformation("Not our single connection, closing");
                    connection.Dispose();
                }

                break;
            case DbMode.Standard:
            case DbMode.SqlExpressUserMode:
                _logger.LogInformation("Closing a standard connection");
                connection.Dispose();
                break;
            default:
                throw new NotSupportedException("Unsupported connection mode.");
        }
    }

    public string MakeParameterName(DbParameter dbParameter)
    {
        return !_dataSourceInfo.SupportsNamedParameters
            ? "?"
            : $"{_dataSourceInfo.ParameterMarker}{dbParameter.ParameterName}";
    }

    public ProcWrappingStyle ProcWrappingStyle => _dataSourceInfo.ProcWrappingStyle;

    public int MaxParameterLimit => _dataSourceInfo.MaxParameterLimit;

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
        cmd.CommandText = "DBCC USEROPTIONS;";

        using var reader = cmd.ExecuteReader();
        var currentSettings = settings.ToDictionary(kvp => kvp.Key, kvp => "OFF");

        while (reader.Read())
        {
            var key = reader.GetString(0).ToUpperInvariant();
            if (settings.ContainsKey(key))
            {
                currentSettings[key] = reader.GetString(1) == "SET" ? "ON" : "OFF";
            }
        }

        var sb = CompareResults(settings, currentSettings);


        if (sb.Length > 0)
        {
            sb.Insert(0, "SET NOCOUNT ON;\n");
            sb.AppendLine(";\nSET NOCOUNT OFF;");
            _connectionSessionSettings = sb.ToString();
        }
    }

    private StringBuilder CompareResults(Dictionary<string, string> expected, Dictionary<string, string> recorded)
    {
        //used for checking which connection/session settings are on or off for mssql
        var sb = new StringBuilder();
        foreach (var expectedKvp in expected)
        {
            recorded.TryGetValue(expectedKvp.Key, out var result);
            if (result != expectedKvp.Value)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.Append($"SET {expectedKvp.Key} {expectedKvp.Value}");
            }
        }

        return sb;
    }

    private void InitializeInternals(string connectionString, DbMode mode, ReadWriteMode readWriteMode)
    {
        DbConnection conn = null;
        try
        {
            _isReadConnection = (readWriteMode & ReadWriteMode.ReadOnly) == ReadWriteMode.ReadOnly;
            _isWriteConnection = (readWriteMode & ReadWriteMode.WriteOnly) == ReadWriteMode.WriteOnly;
            var csb = _factory.CreateConnectionStringBuilder() ?? new DbConnectionStringBuilder();
            conn = _factory.CreateConnection();
            csb.ConnectionString = connectionString;
            conn.ConnectionString = csb.ConnectionString;
            AddStateChangeHandler(conn);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                throw new ConnectionFailedException(ex.Message);
            }

            _dataSourceInfo = new DataSourceInformation(conn);
            SetupPreambleForProvider(conn);
            ConnectionString = csb.ConnectionString;
            this.Name = _dataSourceInfo.DatabaseProductName;
            if (_dataSourceInfo.Product == SupportedDatabase.Sqlite
                && "Data Source=:memory:" == ConnectionString)
            {
                //sqlite memory mode
                ConnectionMode = DbMode.SingleConnection;
                mode = ConnectionMode;
            }

            if (mode != DbMode.Standard)
                // if the mode is anything but standard
                // we store it as our minimal connection
                _connection = conn;
        }
        finally
        {
            if (mode == DbMode.Standard)
                //if it is standard mode, we can close it.
                conn?.Dispose();
        }
    }

    private void SetupPreambleForProvider(DbConnection conn)
    {
        switch (_dataSourceInfo.Product)
        {
            case SupportedDatabase.SqlServer:
                //sets up only what is necessary
                CheckForSqlServerSettings(conn);
                break;

            case SupportedDatabase.MySql:
            case SupportedDatabase.MariaDb:
                _connectionSessionSettings =
                    "SET SESSION sql_mode = 'STRICT_ALL_TABLES,ONLY_FULL_GROUP_BY,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';\n";
                break;

            case SupportedDatabase.PostgreSql:
            case SupportedDatabase.CockroachDb:
                _connectionSessionSettings = @"
                SET standard_conforming_strings = on;
                SET client_min_messages = warning;
                SET search_path = public;
";
                break;

            case SupportedDatabase.Oracle:
                _connectionSessionSettings = @"
                ALTER SESSION SET NLS_DATE_FORMAT = 'YYYY-MM-DD';
                ALTER SESSION SET CURRENT_SCHEMA = your_schema;
";
                break;

            case SupportedDatabase.Sqlite:
                _connectionSessionSettings = "PRAGMA foreign_keys = ON;";
                break;

            case SupportedDatabase.Firebird:
                // _connectionSessionSettings = "SET NAMES UTF8;";
                // has to be done in connection string, not session;
                break;

            case SupportedDatabase.Db2:
                _connectionSessionSettings = @"
                 SET CURRENT DEGREE = 'ANY';
";
                break;

            default:
                _connectionSessionSettings = string.Empty;
                break;
        }

        _applyConnectionSessionSettings = _connectionSessionSettings?.Length > 0;
    }


    private DbConnection GetStandardConnection()
    {
        var conn = _factory.CreateConnection();
        conn.ConnectionString = ConnectionString;
        AddStateChangeHandler(conn);
        return conn;
    }

    public long NumberOfOpenConnections => Interlocked.Read(ref _connectionCount);

    public string QuotePrefix => DataSourceInfo.QuotePrefix;

    public string QuoteSuffix => DataSourceInfo.QuoteSuffix;

    private void AddStateChangeHandler(DbConnection connection)
    {
        connection.StateChange += (sender, args) =>
        {
            switch (args.CurrentState)
            {
                case ConnectionState.Broken:
                    Interlocked.Decrement(ref _connectionCount);
                    break;
                case ConnectionState.Open:
                    Interlocked.Increment(ref _connectionCount);
                    if (args.OriginalState == ConnectionState.Closed)
                    {
                        ApplyConnectionSessionSettings(connection);
                    }

                    break;
                case ConnectionState.Closed:
                    Interlocked.Decrement(ref _connectionCount);
                    break;
                case ConnectionState.Executing:
                case ConnectionState.Fetching:
                    // we aren't interested
                    break;
                case ConnectionState.Connecting:
                    //maybe we care
                    break;
                default:
                    // can't get here
                    break;
            }

            var info = String.Format("{1} now has open connections:{0}", _connectionCount,
                DataSourceInfo?.Product.ToString() ?? String.Empty);
            _logger.LogInformation(info);
        };
    }

    private void ApplyConnectionSessionSettings(DbConnection connection)
    {
        if (_applyConnectionSessionSettings)
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = _connectionSessionSettings;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error setting session settings:" + ex.Message);
                _applyConnectionSessionSettings = false;
            }
    }

    private DbConnection GetSingleConnection()
    {
        return Connection;
    }

    private DbConnection GetSqlCeConnection(ExecutionType type)
    {
        if (ExecutionType.Read == type) return GetStandardConnection();

        return Connection;
    }

    private DbConnection GetSqlExpressUserModeConnection()
    {
        //Just return a new connection, leaving the 1 connection open;
        return GetStandardConnection();
    }


    private void Dispose(bool disposing)
    {
        if (disposing) _connection?.Dispose();
    }

    ~DatabaseContext()
    {
        Dispose(false);
    }
}