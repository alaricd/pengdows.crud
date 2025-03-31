using System.Data;
using System.Data.Common;
using System.Text;
using pengdows.crud.exceptions;

namespace pengdows.crud;

public class DatabaseContext : IDatabaseContext
{
    private static readonly Random _random = new();
    private readonly DbProviderFactory _factory;
    private readonly bool _isReadConnection = true;
    private readonly bool _isWriteConnection = true;
    private DbConnection? _connection;

    private long _connectionCount;
    private DataSourceInformation _dataSourceInfo;
    private bool _isSqlServer;
    private string _missingSqlSettings;

    public string Name { get; set; }

    public DatabaseContext(string connectionString, DbProviderFactory factory, ITypeMapRegistry typeMapRegistry,
        DbMode mode = DbMode.Standard)
    {
        TypeMapRegistry = typeMapRegistry;
        ConnectionMode = mode;
        _factory = factory;
        InitializeInternals(connectionString, mode);
    }

    public DatabaseContext(string connectionString, string providerName, ITypeMapRegistry typeMapRegistry,
        DbMode mode = DbMode.Standard)
    {
        TypeMapRegistry = typeMapRegistry;
        ConnectionMode = mode;
        _factory = DbProviderFactories.GetFactory(providerName);
        InitializeInternals(connectionString, mode);
    }

    private string ConnectionString { get; set; }


    private DbConnection Connection => _connection ??
                                       throw new ObjectDisposedException(
                                           "attempt to use single connection from the wrong mode.");

    public DbMode ConnectionMode { get; private set; }

    public ITypeMapRegistry TypeMapRegistry { get; }

    public IDataSourceInformation DataSourceInfo => _dataSourceInfo;


    public string MissingSqlSettings => _missingSqlSettings ?? "";

    public string WrapObjectName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        var ss = name.Split(_dataSourceInfo.CompositeIdentifierSeparator);
        var qp = _dataSourceInfo.QuotePrefix;
        var qs = _dataSourceInfo.QuoteSuffix;
        if (name.Contains(qp) || name.Contains(qs)) return name; //already wrapped or contains a quote

        var sb = new StringBuilder();
        foreach (var s in ss)
        {
            if (sb.Length > 0) sb.Append(_dataSourceInfo.CompositeIdentifierSeparator);

            sb.Append(qp);
            sb.Append(s);
            sb.Append(qs);
        }

        return sb.ToString();
    }

    public TransactionContext BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        if (!_isWriteConnection && isolationLevel == IsolationLevel.RepeatableRead)
            throw new InvalidOperationException("Read only transactions must be 'RepeatableRead' isolationLevel.");

        return new TransactionContext(this, isolationLevel);
    }

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
            // If the name is not valid, regenerate a new one
            return GenerateRandomName(len);

        return generatedName;
    }

    public DbParameter CreateDbParameter<T>(DbType type, T value)
    {
        return CreateDbParameter(null, type, value);
    }

    public void AssertIsReadConnection()
    {
        if (!_isReadConnection) throw new InvalidOperationException("The connection is not read connection.");
    }

    public void AssertIsWriteConnection()
    {
        if (!_isWriteConnection) throw new InvalidOperationException("The connection is not write connection.");
    }

    public void CloseAndDisposeConnection(DbConnection connection)
    {
        if (connection == null)
        {
            return;
        }

        Console.WriteLine($"Connection mode is: {ConnectionMode}");
        switch (ConnectionMode)
        {   
            case DbMode.SingleConnection:
            case DbMode.SqlCe:
                if (_connection != connection)
                {         
                    //never close our single write connection
                    Console.WriteLine("Not our single connection, closing");    
                    connection.Dispose();
                }

                break;
            case DbMode.Standard:
            case DbMode.SqlExpressUserMode:
                Console.WriteLine("Closing a standard connection");
                connection.Dispose();
                break;
            default:
                throw new NotSupportedException("Unsupported connection mode.");
        }
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
            if (settings.ContainsKey(key)) currentSettings[key] = reader.GetString(1) == "SET" ? "ON" : "OFF";
        }

        var sb = CompareResults(settings, currentSettings);


        if (sb.Length > 0)
        {
            sb.Insert(0, "SET NOCOUNT ON;\n");
            sb.AppendLine(";\nSET NOCOUNT OFF;");
            _missingSqlSettings = sb.ToString();
        }
    }

    private StringBuilder CompareResults(Dictionary<string, string> expected, Dictionary<string, string> recorded)
    {
        var sb = new StringBuilder();
        foreach (var expectedKvp in expected)
        {
            recorded.TryGetValue(expectedKvp.Key, out var result);
            if (result != expectedKvp.Value)
            {
                if (sb.Length > 0) sb.AppendLine();

                sb.Append($"SET {expectedKvp.Key} {expectedKvp.Value}");
            }
        }

        return sb;
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
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                throw new ConnectionFailedException(ex.Message);
            }

            _dataSourceInfo = new DataSourceInformation(conn);
            CheckForSqlServerSettings(conn);
            ConnectionString = csb.ConnectionString;
            this.Name = _dataSourceInfo.DatabaseProductName;
            if ("Data Source=:memory:" == ConnectionString)
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


    private DbConnection GetStandardConnection()
    {
        var conn = _factory.CreateConnection();
        conn.ConnectionString = ConnectionString;
        AddStateChangeHandler(conn);
        return conn;
    }

    public long NumberOfOpenConnections => Interlocked.Read(ref _connectionCount);

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
                    break;
                case ConnectionState.Closed:
                    Interlocked.Decrement(ref _connectionCount);
                    break;
                default:
                    // not important for this 
                    break;
            }

            Console.WriteLine("{1} now has open connections:{0}", _connectionCount, Name);
        };
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