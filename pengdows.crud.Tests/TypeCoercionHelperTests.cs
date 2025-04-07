using System;
using System.Data;
using pengdows.crud.attributes;
using Xunit;
using Xunit.Sdk;

namespace pengdows.crud.Tests;
[Table("Sample")]
public class TestEntity
{
    [Id]
    [Column("Id", DbType.Int32)]
    public int Id { get; set; }
    
    [Column("MaxValue", DbType.Int32)]
    public int MaxValue { get; set; }
}
public class TypeCoercionHelperTests
{
    [Fact]
    public void Coerce_StringToInt_ReturnsInt()
    {
        var typeRegistry = new TypeMapRegistry();
        var ti = typeRegistry.GetTableInfo<TestEntity>();
        ti.Columns.TryGetValue("MaxValue", out var maxValue);
        var result = TypeCoercionHelper.Coerce("123", typeof(string), maxValue);
        Assert.Equal(123, result);
    }

    // [Fact]
    // public void Coerce_InvalidEnum_Throws_WhenModeIsThrow()
    // {
    //     var column = new ColumnInfo { EnumType = typeof(TestEnum) };
    //
    //     Assert.Throws<ArgumentException>(() => TypeCoercionHelper.Coerce("Invalid", typeof(string), typeof(TestEnum)));
    // }

    private enum TestEnum
    {
        A,
        B,
        C
    }
}