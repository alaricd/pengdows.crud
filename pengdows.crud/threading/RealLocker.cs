namespace pengdows.crud.threading;

internal sealed class RealLocker : ILocker
{
    private readonly object _lock;

    public RealLocker(object lockObj)
    {
        _lock = lockObj;
        Monitor.Enter(_lock); // Could use SemaphoreSlim if async
    }

    public void Dispose()
    {
        Monitor.Exit(_lock);
    }
}