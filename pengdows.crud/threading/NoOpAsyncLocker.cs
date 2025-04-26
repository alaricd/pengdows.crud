namespace pengdows.crud.threading;

internal sealed class NoOpAsyncLocker : ILockerAsync
{
    public static readonly NoOpAsyncLocker Instance = new();

    private NoOpAsyncLocker() { }

    public Task LockAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}