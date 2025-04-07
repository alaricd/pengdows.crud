using Xunit;

namespace pengdows.crud.Tests;

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