#region
using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.Extensions.Logging;
using pengdows.crud.enums;
using pengdows.crud.exceptions;
using pengdows.crud.isolation;
using pengdows.crud.wrappers;

#endregion

namespace pengdows.crud;

internal class SessionSettingsManager
{
    private readonly ILogger<IDatabaseContext> _logger;
    private readonly DbProviderFactory _factory;
    private readonly string _connectionString;

    private bool _applyConnectionSessionSettings;
    private string _connectionSessionSettings = string.Empty;
    private bool _isSqlServer;

    public SessionSettingsManager(ILogger<IDatabaseContext> logger, DbProviderFactory factory, string connectionString)
    {
        _logger = logger;
        _factory = factory;
        _connectionString = connectionString;
    }

    public string SessionSettingsPreamble => _connectionSessionSettings ?? string.Empty;
    public bool ApplySessionSettings => _applyConnectionSessionSettings;

    public void CheckForSqlServerSettings(ITrackedConnection conn, DataSourceInformation info)
    {
        _isSqlServer =
            info.DatabaseProductName.StartsWith("Microsoft SQL Server", StringComparison.OrdinalIgnoreCase)
            && !info.DatabaseProductName.Contains("Compact", StringComparison.OrdinalIgnoreCase);

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

    private static StringBuilder CompareResults(Dictionary<string, string> expected, Dictionary<string, string> recorded)
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

    public void SetupConnectionSessionSettingsForProvider(ITrackedConnection conn, DataSourceInformation info)
    {
        switch (info.Product)
        {
            case SupportedDatabase.SqlServer:
                CheckForSqlServerSettings(conn, info);
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

    public void ApplyConnectionSessionSettings(IDbConnection connection)
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

    public DbConnectionStringBuilder GetFactoryConnectionStringBuilder(string connectionString)
    {
        var csb = _factory.CreateConnectionStringBuilder() ?? new DbConnectionStringBuilder();
        csb.ConnectionString = string.IsNullOrEmpty(connectionString) ? _connectionString : connectionString;
        return csb;
    }
}
