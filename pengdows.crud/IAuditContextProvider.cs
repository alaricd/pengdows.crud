namespace pengdows.crud;

public interface IAuditContextProvider<T>
{
    T GetCurrentUserIdentifier();
    DateTime GetUtcNow(); // for testability
}