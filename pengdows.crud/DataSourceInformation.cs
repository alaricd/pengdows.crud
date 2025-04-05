using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace pengdows.crud;

public class DataSourceInformation : IDataSourceInformation
{
    private readonly bool? _supportsNamedParameters;

    public DataSourceInformation(DbConnection connection)
    {
        try
        {
            var metaData = GetSchema(connection);
            var metaDataRow = metaData.Rows[0];

            DatabaseProductName = GetColumnValue<string>(metaDataRow, "DataSourceProductName", "Unknown");
            DatabaseProductVersion = GetColumnValue<string>(metaDataRow, "DataSourceProductVersion", "Unknown");
            Product = InferDatabaseProduct(DatabaseProductName);
            ParameterMarkerPattern = GetColumnValue<string>(metaDataRow, "ParameterMarkerPattern", null);
           _supportsNamedParameters = GetColumnValue<bool?>(metaDataRow, "SupportsNamedParameters");
            ParameterMarker = GetColumnValue<string>(metaDataRow, "ParameterMarkerFormat", "?");
            ParameterNameMaxLength = GetColumnValue(metaDataRow, "ParameterNameMaxLength", 0);
            ParameterNamePatternRegex = new Regex(GetColumnValue<string>(metaDataRow, "ParameterNamePattern", ""));
            CompositeIdentifierSeparator = ".";

            if (Product != SupportedDatabase.Unknown)
            {
                DatabaseProductVersion = GetDatabaseVersion(connection);
                switch (Product)
                {
                    case SupportedDatabase.PostgreSql:
                        Product = (DatabaseProductVersion.Contains("postgres", StringComparison.OrdinalIgnoreCase))
                            ? SupportedDatabase.PostgreSql
                            : SupportedDatabase.CockroachDb;
                        break;
                    case SupportedDatabase.MySql:
                    case SupportedDatabase.MariaDb:
                        Product = DatabaseProductVersion.Contains("maria", StringComparison.OrdinalIgnoreCase)
                            ? SupportedDatabase.MariaDb
                            : SupportedDatabase.MySql;
                        break;
                }
            }

            InferQuoteCharacters();
            ParameterMarker = ExtractParameterMarker();
            _supportsNamedParameters ??= ParameterMarker != "?";
            PrepareStatements = TestPrepareCommand(connection);
            SetupStoredProcWrap();
            MaxParameterLimit = GetMaxParameterLimitFor(Product);
        }
        catch (Exception)
        {
            SetupStoredProcWrap();
            var type = GetConnectionType(connection);
            if (type.Contains("Sqlite"))
            {
                ParameterMarker = "@";
                CompositeIdentifierSeparator = ".";
                ParameterNameMaxLength = 8;
                QuotePrefix = QuoteSuffix = "\"";
                _supportsNamedParameters = true;
                DatabaseProductName = "Sqlite";
                Product = SupportedDatabase.Sqlite;
                ParameterNamePatternRegex =
                    new Regex(@"^[A-Za-z0-9_][A-Za-z0-9_\$]*(?:::?[A-Za-z0-9_\$]*)*(\([^\s)]+\))?$",
                        RegexOptions.Compiled);
                MaxParameterLimit = GetMaxParameterLimitFor(Product);
                ProcWrappingStyle = ProcWrappingStyle.None;
                PrepareStatements = false;
                return;
            }

            throw;
        }
    }

    public string ParameterMarkerPattern { get; set; }

    public string QuotePrefix { get; private set; }
    public string QuoteSuffix { get; private set; }

    public bool SupportsNamedParameters => _supportsNamedParameters ?? false;

    public string ParameterMarker { get; }
    public int ParameterNameMaxLength { get; }
    public Regex ParameterNamePatternRegex { get; }
    public string DatabaseProductName { get; }
    public string DatabaseProductVersion { get; }
    public string CompositeIdentifierSeparator { get; }
    public bool PrepareStatements { get; }
    public ProcWrappingStyle ProcWrappingStyle { get; set; }
    public int MaxParameterLimit { get; }
    public SupportedDatabase Product { get; } = SupportedDatabase.Unknown;

    public bool SupportsMerge => Product switch
    {
        SupportedDatabase.SqlServer => true,
        SupportedDatabase.Oracle => true,
        SupportedDatabase.Firebird => true,
        SupportedDatabase.Db2 => true,
        _ => false
    };

    public bool SupportsInsertOnConflict => Product switch
    {
        SupportedDatabase.PostgreSql => true,
        SupportedDatabase.CockroachDb => true,
        SupportedDatabase.Sqlite => true,
        SupportedDatabase.MySql => true,
        SupportedDatabase.MariaDb => true,
        _ => false
    };

    private static int GetMaxParameterLimitFor(SupportedDatabase db) => db switch
    {
        SupportedDatabase.SqlServer => 2100,
        SupportedDatabase.Sqlite => 999,
        SupportedDatabase.MySql => 65535,
        SupportedDatabase.MariaDb => 65535,
        SupportedDatabase.PostgreSql => 65535,
        SupportedDatabase.CockroachDb => 65535,
        SupportedDatabase.Oracle => 1000,
        SupportedDatabase.Firebird => 1499,
        SupportedDatabase.Db2 => 32767,
        _ => 999
    };

    public string GetDatabaseVersion(DbConnection connection)
    {
        try
        {
            string versionQuery = Product switch
            {
                SupportedDatabase.SqlServer => "SELECT @@VERSION",
                SupportedDatabase.MySql => "SELECT VERSION()",
                SupportedDatabase.MariaDb => "SELECT VERSION()",
                SupportedDatabase.PostgreSql => "SELECT version()",
                SupportedDatabase.CockroachDb => "SELECT version()",
                SupportedDatabase.Oracle => "SELECT * FROM v$version",
                SupportedDatabase.Sqlite => "SELECT sqlite_version()",
                SupportedDatabase.Firebird => "SELECT rdb$get_context('SYSTEM', 'VERSION')",
                _ => string.Empty
            };

            if (string.IsNullOrWhiteSpace(versionQuery)) return "Unknown Database Version";

            using var cmd = connection.CreateCommand();
            cmd.CommandText = versionQuery;
            var version = cmd.ExecuteScalar()?.ToString();
            return version ?? "Unknown Version";
        }
        catch (Exception ex)
        {
            return "Error retrieving version: " + ex.Message;
        }
    }

    public DataTable GetSchema(DbConnection connection) =>
        connection.GetSchema(DbMetaDataCollectionNames.DataSourceInformation);

    public string GetConnectionType(DbConnection connection) => connection.GetType().Name;

    private string ExtractParameterMarker()
    {
        var tmp = ParameterMarker.Replace("{0}", "");
        if (!string.IsNullOrWhiteSpace(tmp))
            return tmp;
        
        if (!String.IsNullOrWhiteSpace(ParameterMarkerPattern))
        {
            tmp = ParameterMarkerPattern.Substring(0, 1);
            if (!string.IsNullOrWhiteSpace(tmp))
                return tmp;
        }

        switch (Product)
        {
            case SupportedDatabase.Firebird:
            case SupportedDatabase.SqlServer:
            case SupportedDatabase.MySql:
            case SupportedDatabase.MariaDb:
                return "@";
            case SupportedDatabase.PostgreSql:
            case SupportedDatabase.CockroachDb:
            case SupportedDatabase.Oracle:
                return ":";
            default:
                return "?";
        }
    }

    private void SetupStoredProcWrap()
    {
        ProcWrappingStyle = Product switch
        {
            SupportedDatabase.SqlServer => ProcWrappingStyle.Exec,
            SupportedDatabase.Oracle => ProcWrappingStyle.Oracle,
            SupportedDatabase.MySql => ProcWrappingStyle.Call,
            SupportedDatabase.MariaDb => ProcWrappingStyle.Call,
            SupportedDatabase.Db2 => ProcWrappingStyle.Call,
            SupportedDatabase.PostgreSql => ProcWrappingStyle.PostgreSQL,
            SupportedDatabase.CockroachDb => ProcWrappingStyle.PostgreSQL,
            SupportedDatabase.Firebird => ProcWrappingStyle.ExecuteProcedure,
            _ => ProcWrappingStyle.None
        };
    }

    private void InferQuoteCharacters()
    {
        (QuotePrefix, QuoteSuffix) = Product switch
        {
            SupportedDatabase.MySql => ("`", "`"),
            SupportedDatabase.MariaDb => ("`", "`"),
            SupportedDatabase.SqlServer => ("[", "]"),
            _ => ("\"", "\"")
        };
    }

    private T GetColumnValue<T>(DataRow row, string columnName, T defaultValue = default)
    {
        try
        {
            var value = row[columnName];
            if (Utils.IsNullOrDbNull(value)) return defaultValue;
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    private bool TestPrepareCommand(DbConnection conn)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1 WHERE 1=1";
            cmd.CommandType = CommandType.Text;
            cmd.Prepare();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private SupportedDatabase InferDatabaseProduct(string productName)
    {
        var name = productName?.ToLowerInvariant() ?? string.Empty;
        if (name.Contains("sql server")) return SupportedDatabase.SqlServer;
        if (name.Contains("mariadb")) return SupportedDatabase.MariaDb;
        if (name.Contains("mysql")) return SupportedDatabase.MySql;
        if (name.Contains("cockroach")) return SupportedDatabase.CockroachDb;
        if (name.Contains("postgres") || name.Contains("npgsql")) return SupportedDatabase.PostgreSql;
        if (name.Contains("oracle")) return SupportedDatabase.Oracle;
        if (name.Contains("sqlite")) return SupportedDatabase.Sqlite;
        if (name.Contains("firebird")) return SupportedDatabase.Firebird;
        if (name.Contains("db2")) return SupportedDatabase.Db2;
        return SupportedDatabase.Unknown;
    }
}