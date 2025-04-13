using System.Data;

namespace pengdows.crud.wrappers;

public interface ITrackedConnection : IDbConnection
{
    IDbTransaction BeginTransaction();
    IDbTransaction BeginTransaction(IsolationLevel isolationLevel);
    void ChangeDatabase(string databaseName);
    void Close();
    IDbCommand CreateCommand();
    void Open();
    Task OpenAsync(CancellationToken cancellationToken = default);
    string ConnectionString { get; set; }
    int ConnectionTimeout { get; }
    string Database { get; }
    ConnectionState State { get; }
    string DataSource { get; }
    string ServerVersion { get; }
    DataTable GetSchema(string dataSourceInformation);
    void Dispose();
    ValueTask DisposeAsync();
}