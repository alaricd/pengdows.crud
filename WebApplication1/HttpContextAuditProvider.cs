using pengdows.crud;

namespace WebApplication1;

public class HttpContextAuditProvider : AuditContextProvider<string>
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextAuditProvider(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }
    
    public override string GetCurrentUserIdentifier()
    {
        var user = _accessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("User not authenticated.");
        }

        //return int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        return user?.Identity?.Name ?? "Unknown";
    }
}
