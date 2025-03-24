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
            var metaData = connection.GetSchema(DbMetaDataCollectionNames.DataSourceInformation);
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
            InferQuoteCharacters();
            ParameterMarker = ExtractParameterMarker();
            PrepareStatements = TestPrepareCommand(connection);
        }
        catch (Exception ex)
        {
            if ( connection.GetType().Name.Contains("Sqlite"))
            {
                //SupportsBatchedQueries = false;
                ParameterMarker = "@";
                CompositeIdentifierSeparator = ".";
                ParameterNameMaxLength = 8;
                QuotePrefix = QuoteSuffix = "\"";
                SupportsNamedParameters = true;
                //SupportsSchemas = false;
                DatabaseProductName = "Sqlite";
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

    private T GetColumnValue<T>(DataRow row, string columnName, T defaultValue = default(T))
    {
        try
        {
            // Check if the column exists and is not DBNull
            var value = row[columnName];
            if (Utils.IsNullOrDbNull(value))
            {
                return defaultValue;
            }

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
            cmd.Prepare();
            return true;
        }
        catch
        {
            return false;
        }
    }
}