using System.Text.Json;
using pengdows.crud.enums;

namespace pengdows.crud;

public static class TypeCoercionHelper
{
    public static object? Coerce(
        object? value,
        Type dbFieldType,
        ColumnInfo columnInfo,
        EnumParseFailureMode parseMode = EnumParseFailureMode.Throw)
    {
        if (Utils.IsNullOrDbNull(value)) return null;

        var targetType = columnInfo?.PropertyInfo?.PropertyType;

        if (dbFieldType == targetType)
            return value;
        // Enum coercion
        if (columnInfo.EnumType != null)
        {
            if (Enum.TryParse(columnInfo.EnumType, value.ToString(), true, out var result))
                return result;

            switch (parseMode)
            {
                case EnumParseFailureMode.Throw:
                    throw new ArgumentException($"Cannot convert value '{value}' to enum {columnInfo.EnumType}.");

                case EnumParseFailureMode.SetNullAndLog:
                    if (Nullable.GetUnderlyingType(targetType) == columnInfo.EnumType)
                        return null;
                    throw new ArgumentException(
                        $"Cannot convert '{value}' to non-nullable enum {columnInfo.EnumType}.");

                case EnumParseFailureMode.SetDefaultValue:
                    return Enum.GetValues(columnInfo.EnumType).GetValue(0);
            }
        }

        // JSON deserialization
        if (columnInfo.IsJsonType)
        {
            if (value is string json && !string.IsNullOrWhiteSpace(json))
                return JsonSerializer.Deserialize(json, targetType, columnInfo.JsonSerializerOptions);
            throw new ArgumentException($"Cannot deserialize JSON value '{value}' to type {targetType}.");
        }

        return CoerceCore(value, dbFieldType, targetType);
    }

    public static object? Coerce(
        object? value,
        Type sourceType,
        Type targetType)
    {
        if (Utils.IsNullOrDbNull(value)) return null;

        if (sourceType == targetType)
            return value;

        return CoerceCore(value, sourceType, targetType);
    }

    private static object? CoerceCore(object value, Type sourceType, Type targetType)
    {
        // Guid from string or byte[]
        if (targetType == typeof(Guid))
        {
            if (value is string guidStr && Guid.TryParse(guidStr, out var guid))
                return guid;
            if (value is byte[] bytes && bytes.Length == 16)
                return new Guid(bytes);
        }

        // DateTime from string
        if (sourceType == typeof(string) && targetType == typeof(DateTime) && value is string s)
            return DateTime.Parse(s);

        try
        {
            var underlyingTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;
            return Convert.ChangeType(value, underlyingTarget);
        }
        catch (Exception ex)
        {
            throw new InvalidCastException($"Cannot convert value '{value}' ({sourceType}) to {targetType}.", ex);
        }
    }
}