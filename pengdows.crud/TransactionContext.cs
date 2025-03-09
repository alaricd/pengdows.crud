using System.Data;
using System.Data.Common;

namespace pengdows.crud;
public class TransactionContext : IDbContext
{
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private readonly DataSourceInformation _dataSourceInfo;
    private bool _committed = false;
    private bool _disposed = false;

    public TransactionContext(DbConnection connection, DataSourceInformation dataSourceInfo)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _dataSourceInfo = dataSourceInfo ?? throw new ArgumentNullException(nameof(dataSourceInfo));
        
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        _transaction = _connection.BeginTransaction();
    }

    public SqlContainer CreateSqlContainer() => new SqlContainer(this);

    public void Commit()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TransactionContext));
        if (_committed) return;

        _transaction.Commit();
        _committed = true;
    }

    public void Rollback()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TransactionContext));
        if (_committed) return;

        _transaction.Rollback();
        _committed = true;
    }

    public DbConnection Connection => _connection;
    public DbTransaction Transaction => _transaction;
    public DataSourceInformation DataSourceInfo => _dataSourceInfo;

    public void Dispose()
    {
        if (_disposed) return;

        if (!_committed)
        {
            try
            {
                _transaction.Rollback();
            }
            catch (Exception ex)
            {
                // Log exception for diagnostics if needed.
                Console.Error.WriteLine($"Failed to rollback transaction: {ex}");
            }
        }

        _transaction.Dispose();
        _connection.Dispose();
        _disposed = true;
    }
}
