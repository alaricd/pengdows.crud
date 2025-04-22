using System.Collections.Concurrent;
using System.Data.Common;

namespace pengdows.crud;

public class TenantContextRegistry 
{
    private readonly ConcurrentDictionary<string, IDatabaseContext> _contexts = new();
    private readonly ITenantConnectionResolver _resolver;
    private readonly DbProviderFactory _providerFactory;

    public TenantContextRegistry(ITenantConnectionResolver resolver, DbProviderFactory providerFactory)
    {
        _resolver = resolver;
        _providerFactory = providerFactory;
    }

    public IDatabaseContext GetContext(string tenant)
    {
        return _contexts.GetOrAdd(tenant, t =>
        {
            var connectionString = _resolver.GetConnectionString(t);
            return new DatabaseContext(connectionString, _providerFactory);
        });
    }
}