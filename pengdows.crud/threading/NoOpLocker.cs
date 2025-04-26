namespace pengdows.crud.threading;

internal sealed class NoOpLocker : ILocker
{
    public static readonly NoOpLocker Instance = new();
    private NoOpLocker() { }

    public void Dispose() { /* no-op */ }
}