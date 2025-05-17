#region

using Microsoft.Extensions.DependencyInjection;

#endregion

namespace pengdows.crud;
public class AuditFieldResolver<TUserId>
{
    private readonly IAuditContextProvider<TUserId> _provider;

    public AuditFieldResolver(IAuditContextProvider<TUserId> provider)
    {
        _provider = provider;
    }

    public (TUserId userId, DateTime utcNow) Resolve()
    {
        return (_provider.GetCurrentUserIdentifier(), _provider.GetUtcNow());
    }
}
