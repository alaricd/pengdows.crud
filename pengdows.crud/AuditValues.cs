namespace pengdows.crud;

public sealed class AuditValues : IAuditValues
{
    public required object UserId { get; init; }
    public required DateTime UtcNow { get; init; }

    public T As<T>() => (T)UserId;
}