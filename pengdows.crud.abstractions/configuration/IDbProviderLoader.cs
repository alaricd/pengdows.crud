using Microsoft.Extensions.DependencyInjection;

namespace pengdows.crud.configuration;

public interface IDbProviderLoader
{
    void LoadAndRegisterProviders(IServiceCollection services);
}