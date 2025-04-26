namespace pengdows.crud.threading;

internal sealed class RealAsyncLocker : ILockerAsync
{
    private readonly SemaphoreSlim _semaphore;

    public RealAsyncLocker(SemaphoreSlim semaphore)
    {
        _semaphore = semaphore;
    }

    public async Task LockAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        _semaphore.Release();
        return ValueTask.CompletedTask;
    }
}