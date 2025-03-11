using System.Data.Common;
using System.Text.RegularExpressions;

namespace pengdows.crud;

public class DataSourceInformation : IDataSourceInformation
{
    public DataSourceInformation(DbConnection connection)
    {
        var metaData = connection.GetSchema(DbMetaDataCollectionNames.DataSourceInformation);
        QuotePrefix = metaData.Rows[0]["QuotedIdentifierPattern"].ToString()?.Substring(0, 1) ?? "\"";
        QuoteSuffix = QuotePrefix;
        SupportsNamedParameters = Convert.ToBoolean(metaData.Rows[0]["SupportsNamedParameters"] ?? false);
        ParameterMarker = metaData.Rows[0]["ParameterMarkerFormat"].ToString() ?? "?";
        ParameterNameMaxLength = Convert.ToInt32(metaData.Rows[0]["ParameterNameMaxLength"] ?? 0);
        ParameterNamePatternRegex = new Regex(metaData.Rows[0]["ParameterNamePattern"].ToString() ?? "");
        DatabaseProductName = metaData.Rows[0]["DataSourceProductName"].ToString() ?? "Unknown";
        DatabaseProductVersion = metaData.Rows[0]["DataSourceProductVersion"].ToString() ?? "Unknown";
        SchemaSeparator = metaData.Rows[0]["SchemaSeparator"]?.ToString() ?? ".";

        PrepareStatements = TestPrepareCommand(connection);
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

    public string QuotePrefix { get; }
    public string QuoteSuffix { get; }
    public bool SupportsNamedParameters { get; }
    public string ParameterMarker { get; }
    public int ParameterNameMaxLength { get; }
    public Regex ParameterNamePatternRegex { get; }
    public string DatabaseProductName { get; }
    public string DatabaseProductVersion { get; }
    public string SchemaSeparator { get; }
    public bool PrepareStatements { get; set; }
}