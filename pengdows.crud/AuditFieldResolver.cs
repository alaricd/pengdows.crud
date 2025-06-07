namespace pengdows.crud;

public abstract class AuditValueResolver : IAuditFieldResolver
{
    public abstract IAuditValues Resolve();
}