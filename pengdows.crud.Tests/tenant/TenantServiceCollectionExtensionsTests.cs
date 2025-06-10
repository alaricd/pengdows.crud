using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using pengdows.crud.configuration;
using pengdows.crud.tenant;
using Xunit;

namespace pengdows.crud.Tests.tenant;

public class TenantServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMultiTenancy_BindsConfigurationAndRegistersServices()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:0:Name"] = "a",
                ["Tenants:0:DatabaseContextConfiguration:ConnectionString"] = "Server=A;",
                ["Tenants:1:Name"] = "b",
                ["Tenants:1:DatabaseContextConfiguration:ConnectionString"] = "Server=B;"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMultiTenancy(config);

        var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<ITenantConnectionResolver>();
        var contextRegistry = provider.GetRequiredService<ITenantContextRegistry>();

        Assert.Equal("Server=A;", resolver.GetDatabaseContextConfiguration("a").ConnectionString);
        Assert.Equal("Server=B;", resolver.GetDatabaseContextConfiguration("b").ConnectionString);
        Assert.NotNull(contextRegistry);
    }
}
