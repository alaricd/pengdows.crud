namespace WebApplication1;

public class HttpContextAuditProvider : IAuditContextProvider<int>
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextAuditProvider(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public int GetCurrentUserId()
    {
        var user = _accessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("User not authenticated.");
        }

        return int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    }
}
