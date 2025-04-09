#region

using System.Data.Common;
using System.Text.RegularExpressions;

#endregion

namespace pengdows.crud;

public interface IDataSourceInformation
{
    string QuotePrefix { get; }
    string QuoteSuffix { get; }
    bool SupportsNamedParameters { get; }
    string ParameterMarker { get; }
    int ParameterNameMaxLength { get; }
    Regex ParameterNamePatternRegex { get; }
    string DatabaseProductName { get; }
    string DatabaseProductVersion { get; }
    string CompositeIdentifierSeparator { get; }
    bool PrepareStatements { get; }
    ProcWrappingStyle ProcWrappingStyle { get; }
    int MaxParameterLimit { get; }
    SupportedDatabase Product { get; }
    bool SupportsMerge { get; }
    bool SupportsInsertOnConflict { get; }
    string GetDatabaseVersion(DbConnection dbConnection);
}