namespace pengdows.crud;

public sealed class AuditValues : IAuditValues
{
    public required DateTime UtcNow { get; init; }
    public required object UserId { get; init; }

    public T As<T>()
    {
        return (T)UserId;
    }
}