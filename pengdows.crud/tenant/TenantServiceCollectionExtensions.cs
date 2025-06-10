using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.crud.tenant;

public static class TenantServiceCollectionExtensions
{
    public static IServiceCollection AddMultiTenancy(this IServiceCollection services, IConfiguration configuration)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var options = new MultiTenantOptions();
        configuration.Bind(options);

        TenantConnectionResolver.Register(options.Tenants);

        services.Configure<MultiTenantOptions>(configuration);
        services.AddSingleton<ITenantConnectionResolver>(TenantConnectionResolver.Instance);
        services.AddSingleton<ITenantContextRegistry, TenantContextRegistry>();

        return services;
    }
}
