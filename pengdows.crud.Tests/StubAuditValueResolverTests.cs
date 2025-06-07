namespace pengdows.crud.Tests;

using System;
using Xunit;

 

public class StubAuditValueResolverTests
{
    [Fact]
    public void Resolve_ReturnsConfiguredValues()
    {
        var resolver = new StubAuditValueResolver("test-user");
        var values = resolver.Resolve();

        Assert.Equal("test-user", values.UserId);
        Assert.True(values.UtcNow > DateTime.MinValue);
    }
}