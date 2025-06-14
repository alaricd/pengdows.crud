#region
using pengdows.crud;

#endregion

namespace WebApplication1;

public class HttpContextAuditValueResolver : AuditValueResolver
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextAuditValueResolver(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public override IAuditValues Resolve()
    {
        var user = _accessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            throw new InvalidOperationException("User not authenticated.");

        return new AuditValues
        {
            UserId = user.Identity?.Name ?? "Unknown",
            UtcNow = DateTime.UtcNow
        };
    }
}
