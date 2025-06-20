#region
using System.Data;
using System.Data.Common;
using pengdows.crud.enums;
using pengdows.crud.wrappers;
#endregion

namespace pengdows.crud;

internal class ParameterFactory
{
    private readonly DbProviderFactory _factory;
    private readonly IDataSourceInformation _info;

    public ParameterFactory(DbProviderFactory factory, IDataSourceInformation info)
    {
        _factory = factory;
        _info = info;
    }

    public DbParameter CreateDbParameter<T>(string? name, DbType type, T value)
    {
        var p = _factory.CreateParameter() ?? throw new InvalidOperationException("Failed to create parameter.");

        if (string.IsNullOrWhiteSpace(name)) name = GenerateRandomName();

        var valueIsNull = Utils.IsNullOrDbNull(value);
        p.ParameterName = name;
        p.DbType = type;
        p.Value = valueIsNull ? DBNull.Value : value;
        if (!valueIsNull && p.DbType == DbType.String && value is string s) p.Size = Math.Max(s.Length, 1);

        return p;
    }

    public DbParameter CreateDbParameter<T>(DbType type, T value)
    {
        return CreateDbParameter(null, type, value);
    }

    public string GenerateRandomName(int length = 5, int parameterNameMaxLength = 30)
    {
        var validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_".ToCharArray();
        var len = Math.Min(Math.Max(length, 2), parameterNameMaxLength);

        Span<char> buffer = stackalloc char[len];
        const int firstCharMax = 52; // a-zA-Z
        var anyOtherMax = validChars.Length;

        buffer[0] = validChars[Random.Shared.Next(firstCharMax)];
        for (var i = 1; i < len; i++) buffer[i] = validChars[Random.Shared.Next(anyOtherMax)];

        return new string(buffer);
    }

    public string MakeParameterName(DbParameter dbParameter)
    {
        return MakeParameterName(dbParameter.ParameterName);
    }

    public string MakeParameterName(string parameterName)
    {
        return !_info.SupportsNamedParameters
            ? "?"
            : $"{_info.ParameterMarker}{parameterName}";
    }

    public ProcWrappingStyle ProcWrappingStyle
    {
        get => _info.ProcWrappingStyle;
       // set => _info.ProcWrappingStyle = value;
    }

    public int MaxParameterLimit => _info.MaxParameterLimit;
}
