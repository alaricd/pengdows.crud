using System;
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
}