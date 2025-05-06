namespace pengdows.crud.infrastructure;

using System;
using System.Threading;
using System.Threading.Tasks;

public abstract class SafeAsyncDisposableBase : ISafeAsyncDisposableBase
{
    private int _disposed;

    public bool IsDisposed => Interlocked.CompareExchange(ref _disposed, 0, 0) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            DisposeManaged();
            DisposeUnmanaged();
        }
        catch
        {
            // optionally log
        }

        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await DisposeManagedAsync().ConfigureAwait(false);
            DisposeUnmanaged();
        }
        catch
        {
            // optionally log
        }

        GC.SuppressFinalize(this);
    }

    protected virtual void DisposeManaged()
    {
        // sync fallback
    }

    protected virtual async ValueTask DisposeManagedAsync()
    {
        DisposeManaged();
        await Task.CompletedTask;
    }

    protected virtual void DisposeUnmanaged()
    {
        // for unmanaged handle cleanup if needed
    }
}