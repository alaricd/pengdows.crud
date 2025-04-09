#region

using Microsoft.Extensions.DependencyInjection;

#endregion

namespace pengdows.crud;

public static class AuditFieldResolver
{
    public static (object userId, DateTime utcNow) ResolveFrom(
        Type userFieldType,
        IServiceProvider services)
    {
        var providerType = typeof(IAuditContextProvider<>).MakeGenericType(userFieldType);

        try
        {
            dynamic provider = services.GetRequiredService(providerType);
            return (provider.GetCurrentUserIdentifier(), provider.GetUtcNow());
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"Audit provider for user type '{userFieldType.Name}' is missing. " +
                $"Ensure IAuditContextProvider<{userFieldType.Name}> is registered with DI.", ex);
        }
    }
}