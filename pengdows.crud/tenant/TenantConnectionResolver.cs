using System.Collections.Concurrent;
using pengdows.crud.configuration;

namespace pengdows.crud.tenant;

public class TenantConnectionResolver : ITenantConnectionResolver
{
    public static ITenantConnectionResolver Instance { get; } = new TenantConnectionResolver();

    private static readonly ConcurrentDictionary<string, DatabaseContextConfiguration> _configurations = new();

    public IDatabaseContextConfiguration GetDatabaseContextConfiguration(string tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant))
        {
            throw new ArgumentNullException(nameof(tenant), "Tenant ID must not be null or empty.");
        }

        if (!_configurations.TryGetValue(tenant, out var config))
        {
            throw new InvalidOperationException($"No database configuration registered for tenant '{tenant}'.");
        }

        return config;
    }

    public static void Register(string tenant, DatabaseContextConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(tenant))
        {
            throw new ArgumentNullException(nameof(tenant));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        _configurations[tenant] = configuration;
    }
}
