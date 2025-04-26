namespace pengdows.crud;

public abstract class AuditContextProvider<TUserId>
    : IAuditContextProvider<TUserId>
{
    public abstract TUserId GetCurrentUserIdentifier();

    public DateTime GetUtcNow()
    {
        return DateTime.UtcNow;
    }
}