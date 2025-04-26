namespace pengdows.crud.threading;

internal interface ILockerAsync : IAsyncDisposable
{
    Task LockAsync();
}