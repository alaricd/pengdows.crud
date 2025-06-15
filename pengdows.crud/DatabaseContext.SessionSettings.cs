#region
using System.Data;
using System.Data.Common;
using System.Text;
using pengdows.crud.configuration;
using pengdows.crud.enums;
using pengdows.crud.exceptions;
using pengdows.crud.isolation;
#endregion

namespace pengdows.crud;

public partial class DatabaseContext
{
    private void CheckForSqlServerSettings(ITrackedConnection conn)
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
            _connectionSessionSettings = sb.ToString();
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

    private void InitializeInternals(IDatabaseContextConfiguration config)
    {
        var connectionString = config.ConnectionString;
        var mode = config.DbMode;
        var readWriteMode = config.ReadWriteMode;
        ITrackedConnection conn = null;
        try
        {
            _isReadConnection = (readWriteMode & ReadWriteMode.ReadOnly) == ReadWriteMode.ReadOnly;
            _isWriteConnection = (readWriteMode & ReadWriteMode.WriteOnly) == ReadWriteMode.WriteOnly;
            conn = FactoryCreateConnection(connectionString, true);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                throw new ConnectionFailedException(ex.Message);
            }

            _dataSourceInfo = DataSourceInformation.Create(conn);
            SetupConnectionSessionSettingsForProvider(conn);
            Name = _dataSourceInfo.DatabaseProductName;
            if (_dataSourceInfo.Product == SupportedDatabase.Sqlite)
            {
                var csb = GetFactoryConnectionStringBuilder(string.Empty);
                var ds = csb["Data Source"] as string;
                ConnectionMode = ":memory:" == ds ? DbMode.SingleConnection : DbMode.SingleWriter;
                mode = ConnectionMode;
            }

            if (mode != DbMode.Standard)
            {
                ApplyConnectionSessionSettings(conn);
                _connection = conn;
            }
        }
        finally
        {
            _isolationResolver ??= new IsolationResolver(Product, RCSIEnabled);
            if (mode == DbMode.Standard)
                conn?.Dispose();
        }
    }

    private DbConnectionStringBuilder GetFactoryConnectionStringBuilder(string connectionString)
    {
        var csb = _factory.CreateConnectionStringBuilder() ?? new DbConnectionStringBuilder();
        csb.ConnectionString = string.IsNullOrEmpty(connectionString) ? _connectionString : connectionString;
        return csb;
    }

    private void SetupConnectionSessionSettingsForProvider(ITrackedConnection conn)
    {
        switch (_dataSourceInfo.Product)
        {
            case SupportedDatabase.SqlServer:
                CheckForSqlServerSettings(conn);
                break;
            case SupportedDatabase.MySql:
            case SupportedDatabase.MariaDb:
                _connectionSessionSettings =
                    "SET SESSION sql_mode = 'STRICT_ALL_TABLES,ONLY_FULL_GROUP_BY,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION,ANSI_QUOTES';\n";
                break;
            case SupportedDatabase.PostgreSql:
            case SupportedDatabase.CockroachDb:
                _connectionSessionSettings = @"\n                SET standard_conforming_strings = on;\n                SET client_min_messages = warning;\n                SET search_path = public;\n";
                break;
            case SupportedDatabase.Oracle:
                _connectionSessionSettings = @"\n                ALTER SESSION SET NLS_DATE_FORMAT = 'YYYY-MM-DD';\n";
                break;
            case SupportedDatabase.Sqlite:
                _connectionSessionSettings = "PRAGMA foreign_keys = ON;";
                break;
            case SupportedDatabase.Firebird:
                break;
            default:
                _connectionSessionSettings = string.Empty;
                break;
        }

        _applyConnectionSessionSettings = _connectionSessionSettings?.Length > 0;
    }

    private void ApplyConnectionSessionSettings(IDbConnection connection)
    {
        _logger.LogInformation("Applying connection session settings");
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
}
