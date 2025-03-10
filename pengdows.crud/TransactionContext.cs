using System.Data;
using System.Data.Common;

namespace pengdows.crud;

public class TransactionContext : DbContext
{
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private readonly DataSourceInformation _dataSourceInfo;
    private bool _committed;
    private bool _disposed;

    public TransactionContext(DatabaseContext context)
    {
        _connection = context.GetConnection(ExecutionType.Write);
        _dataSourceInfo = context.DataSourceInfo;
        ConnectionMode = context.ConnectionMode;
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }

        _transaction = _connection.BeginTransaction();
    }

    public override TransactionContext BeginTransaction() => this;
      public DbMode ConnectionMode;
    public DbConnection Connection;
    public DataSourceInformation DataSourceInfo;

    public SqlContainer CreateSqlContainer() => new(this);

    public override DbConnection GetConnection(ExecutionType type) => _connection;


    public void AddStateChangeHandler(DbConnection connection)
    {
        connection.StateChange += (sender, args) =>
        {
            if (args.CurrentState == ConnectionState.Closed)
            {
                Console.WriteLine("Transaction connection closed.");
            }
        };
    }

    public string WrapObjectName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var parts = name.Split(DataSourceInfo.SchemaSeparator);
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = DataSourceInfo.QuotePrefix + parts[i] + DataSourceInfo.QuoteSuffix;
        }
        return string.Join(DataSourceInfo.SchemaSeparator, parts);
    }

    public void Commit()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TransactionContext));
        }

        if (_committed)
        {
            return;
        }

        _transaction.Commit();
        _committed = true;
    }

    public void Rollback()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TransactionContext));
        }

        if (_committed)
        {
            return;
        }

        _transaction.Rollback();
        _committed = true;
    }

    protected  void Dispose(bool disposing)
    {
       
        
       base.Dispose(disposing, () =>
       {
           this.Rollback();
       });
    }
}
