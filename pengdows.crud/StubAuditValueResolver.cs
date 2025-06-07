namespace pengdows.crud;

public class StubAuditValueResolver : IAuditValueResolver
{
    private readonly object _userId;

    public StubAuditValueResolver(object userId)
    {
        _userId = userId;
    }

    public IAuditValues Resolve()
    {
        return new AuditValues
        {
            UserId = _userId,
            UtcNow = DateTime.UtcNow
        };
    }
}