using System.Text.RegularExpressions;

namespace pengdows.crud;

public interface IDataSourceInformation
{
    string QuotePrefix { get; }
    string QuoteSuffix { get; }
    bool SupportsNamedParameters { get; }
    string ParameterMarker { get; }
    int ParameterNameMaxLength { get; }
    Regex ParameterNamePatternRegex { get; }
}