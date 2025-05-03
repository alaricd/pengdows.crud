using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.crud.tenant;

public class TenantContextRegistry : ITenantContextRegistry
{
    private readonly ITenantConnectionResolver _resolver;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, IDatabaseContext> _contexts = new();

    public TenantContextRegistry(ITenantConnectionResolver resolver, IServiceProvider serviceProvider)
    {
        _resolver = resolver;
        _serviceProvider = serviceProvider;
    }

    public IDatabaseContext GetContext(string tenant)
    {
        return _contexts.GetOrAdd(tenant, CreateDatabaseContext);
    }

    private IDatabaseContext CreateDatabaseContext(string tenant)
    {
        var tenantInfo = _resolver.GetTenantInfo(tenant); // contains connection string + SupportedDatabase

        var factory = _serviceProvider.GetKeyedService<DbProviderFactory>(tenantInfo.DatabaseType)
                      ?? throw new InvalidOperationException($"No factory registered for {tenantInfo.DatabaseType}");

        return new DatabaseContext(tenantInfo.ConnectionString, factory);
    }
}