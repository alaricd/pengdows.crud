using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace pengdows.crud;

public class DataSourceInformation : IDataSourceInformation
{
    public DataSourceInformation(DbConnection connection)
    {
        try
        {
            var metaData = GetSchema(connection);
            var metaDataRow = metaData.Rows[0];
            // foreach (DataColumn col in metaData.Columns)
            // {
            //     Console.WriteLine($"{col.ColumnName}:{metaDataRow[col.ColumnName]}");
            // }
            SupportsNamedParameters = GetColumnValue<bool>(metaDataRow, "SupportsNamedParameters");
            ParameterMarker = GetColumnValue<string>(metaDataRow, "ParameterMarkerFormat", "?");
            ParameterNameMaxLength = GetColumnValue(metaDataRow, "ParameterNameMaxLength", 0);
            ParameterNamePatternRegex = new Regex(GetColumnValue<string>(metaDataRow, "ParameterNamePattern", ""));
            DatabaseProductName = GetColumnValue<string>(metaDataRow, "DataSourceProductName", "Unknown");
            //if(DatabaseProductName == "Unknown")
            DatabaseProductVersion = GetColumnValue<string>(metaDataRow, "DataSourceProductVersion", "Unknown");
            //Schema and Catalog Separators were combined into this, not every provider gives use this value,
            //but everything seems to use a period.  Leaving it just in case we need to parse it later.
            CompositeIdentifierSeparator = ".";
            GetDatabaseVersion(connection);
            InferQuoteCharacters();
            ParameterMarker = ExtractParameterMarker();
            PrepareStatements = TestPrepareCommand(connection);
            SetupStoredProcWrap();
        }
        catch (Exception ex)
        {
            SetupStoredProcWrap();
            var type = GetConnectionType(connection);
            if (type.Contains("Sqlite"))
            {
                //SupportsBatchedQueries = false;
                ParameterMarker = "@";
                CompositeIdentifierSeparator = ".";
                ParameterNameMaxLength = 8;
                QuotePrefix = QuoteSuffix = "\"";
                SupportsNamedParameters = true;
                //SupportsSchemas = false;
                DatabaseProductName = "Sqlite";
                ParameterNamePatternRegex = new Regex(@"^[A-Za-z0-9_][A-Za-z0-9_\$]*(?:::?[A-Za-z0-9_\$]*)*(\([^\s)]+\))?$");
                return;
            }

            throw;
        }
    }

    public string QuotePrefix { get; private set; }
    public string QuoteSuffix { get; private set; }
    public bool SupportsNamedParameters { get; }
    public string ParameterMarker { get; }
    public int ParameterNameMaxLength { get; }
    public Regex ParameterNamePatternRegex { get; }
    public string DatabaseProductName { get; }
    public string DatabaseProductVersion { get; }
    public string CompositeIdentifierSeparator { get; }
    public bool PrepareStatements { get; }

    public ProcWrappingStyle ProcWrappingStyle { get; set; }

    public int MaxParameterLimit { get; } = 999;

    public string GetDatabaseVersion(DbConnection connection)
    {
        try
        {
            var versionQuery = string.Empty;

            // Adjust the query based on the database type
            var productName = DatabaseProductName?.ToLowerInvariant();
            if (productName != null)
            {
                if (productName.Contains("sql server"))
                    versionQuery = "SELECT @@VERSION"; // SQL Server version query
                else if (productName.Contains("mysql") || productName.Contains("mariadb"))
                    versionQuery = "SELECT VERSION()"; // MySQL version query
                else if (productName.Contains("postgres") || productName.Contains("npgsql"))
                    versionQuery = "SELECT version()"; // PostgreSQL version query
                else if (productName.Contains("oracle"))
                    versionQuery = "SELECT * FROM v$version"; // Oracle version query
                else if (productName.Contains("sqlite"))
                    versionQuery = "SELECT sqlite_version()"; // SQLite version query
                else if (productName.Contains("firebird"))
                    versionQuery = "SELECT rdb$get_context('SYSTEM', 'VERSION')"; // Firebird version query
            }
            // Add other databases as needed

            if (string.IsNullOrWhiteSpace(versionQuery)) return "Unknown Database Version";

            // Execute the version query
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = versionQuery;
                var version = cmd.ExecuteScalar()?.ToString();
                return version ?? "Unknown Version";
            }
        }
        catch (Exception ex)
        {
            // Handle the exception (log it, rethrow, etc.)
            return "Error retrieving version: " + ex.Message;
        }
    }

    public DataTable GetSchema(DbConnection connection)
    {
        return connection.GetSchema(DbMetaDataCollectionNames.DataSourceInformation);
    }

    public string GetConnectionType(DbConnection connection)
    {
        return connection.GetType().Name;
    }


    private string ExtractParameterMarker()
    {
        var parameterMarker = ParameterMarker.Replace("{0}", "");
        if (parameterMarker.Length > 0) return parameterMarker;
        var productName = DatabaseProductName?.ToLowerInvariant() ?? string.Empty;
        if (productName.Contains("mysql") || productName.Contains("sql server"))
            return "@";
        if (productName.Contains("postgres") ||
            productName.Contains("npgsql") ||
            productName.Contains("oracle"))
            return ":";
        //if all else fails, assume no named parameters.
        return "?";
    }

    private void SetupStoredProcWrap()
    {
        var productName = DatabaseProductName?.ToLowerInvariant();

        ProcWrappingStyle = ProcWrappingStyle.None;

        if (string.IsNullOrWhiteSpace(productName))
            return;

        if (productName.Contains("sql server"))
            ProcWrappingStyle = ProcWrappingStyle.Exec; // Uses EXEC procName
        else if (productName.Contains("oracle"))
            ProcWrappingStyle = ProcWrappingStyle.Oracle; // Uses BEGIN procName END;
        else if (productName.Contains("mysql") ||
                 productName.Contains("mariadb") ||
                 productName.Contains("db2"))
            ProcWrappingStyle = ProcWrappingStyle.Call; // Uses CALL procName
        else if (productName.Contains("postgres") || productName.Contains("npgsql"))
            ProcWrappingStyle = ProcWrappingStyle.PostgreSQL; // Decide between SELECT * FROM func() vs CALL proc()
        else if (productName.Contains("firebird")) ProcWrappingStyle = ProcWrappingStyle.ExecuteProcedure;
        // Optional: Add Firebird, Sybase, etc., later
    }


    private void InferQuoteCharacters()
    {
        var productName = DatabaseProductName?.ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(productName))
        {
            //(([^\`]|\`\`)*)
            if (productName.Contains("mysql"))
            {
                QuotePrefix = QuoteSuffix = "`";
                return;
            }

            if (productName.Contains("sql server"))
            {
                QuotePrefix = "[";
                QuoteSuffix = "]";
                return;
            }
        }
        //everything else or unknown


        QuotePrefix = QuoteSuffix = "\"";
    }

    private T GetColumnValue<T>(DataRow row, string columnName, T defaultValue = default)
    {
        try
        {
            // Check if the column exists and is not DBNull
            var value = row[columnName];
            if (Utils.IsNullOrDbNull(value)) return defaultValue;

            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception ex)
        {
            // Log or handle the exception if needed
            // For now, we're returning the default value
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
}