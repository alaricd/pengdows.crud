namespace pengdows.crud;

public interface IAuditValues
{
    object UserId { get; init; }
    DateTime UtcNow { get; init; }

    T As<T>() => (T)UserId;
}