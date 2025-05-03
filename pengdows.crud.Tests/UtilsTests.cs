using System;
using System.Reflection;
using Xunit;

namespace pengdows.crud.Tests;

public class UtilsTests
{
    [Fact]
    public void IsNullOrDbNull_ReturnsTrueForNull()
    {
        Assert.True(Utils.IsNullOrDbNull(null));
        Assert.True(Utils.IsNullOrDbNull(DBNull.Value));
    }

    [Fact]
    public void IsZeroNumeric_ReturnsTrueForZero()
    {
        Assert.True(Utils.IsZeroNumeric(0));
        Assert.True(Utils.IsZeroNumeric(0.0));
        Assert.False(Utils.IsZeroNumeric(1));
    }
    
    public static TAttribute GetAttributeFromProperty<TAttribute>(
        Type containerType,
        string nestedClassName,
        string propertyName
    ) where TAttribute : Attribute
    {
        var nestedType = containerType.GetNestedType(nestedClassName, BindingFlags.NonPublic | BindingFlags.Public);
        if (nestedType == null)
            throw new ArgumentException($"Nested class '{nestedClassName}' not found in '{containerType.Name}'.");

        var propInfo = nestedType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (propInfo == null)
            throw new ArgumentException($"Property '{propertyName}' not found in '{nestedClassName}'.");

        var attr = propInfo.GetCustomAttribute<TAttribute>();
        if (attr == null)
            throw new ArgumentException($"Attribute '{typeof(TAttribute).Name}' not found on property '{propertyName}'.");

        return attr;
    }
    
}