namespace pengdows.crud;

public class StubAuditFieldResolver : IAuditFieldResolver
{
    private readonly object _userId;

    public StubAuditFieldResolver(object userId)
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