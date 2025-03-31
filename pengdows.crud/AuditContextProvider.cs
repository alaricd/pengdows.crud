namespace pengdows.crud;

public abstract class AuditContextProvider<T>
    : IAuditContextProvider<T>
{
    public abstract T GetCurrentUserIdentifier();

    public DateTime GetUtcNow()
    {
        return DateTime.UtcNow;
    }
}