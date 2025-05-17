namespace pengdows.crud;

public interface IAuditContextProvider<TUserId>
{
    TUserId GetCurrentUserIdentifier();
    DateTime GetUtcNow(); // for testability
}
