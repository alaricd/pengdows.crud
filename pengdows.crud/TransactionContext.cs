using System;
using System.Data;
using System.Data.Common;

namespace pengdows.crud;

public class TransactionContext :  ITransactionContext
{
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
     private bool _committed = false;
    private long _disposed = 0;
    private readonly IDatabaseContext _context;

    public TransactionContext(IDatabaseContext context)
    {
        _context = context;
        _connection = _context.GetConnection(ExecutionType.Write);
        EnsureConnectionIsOpen();
        _transaction = _connection.BeginTransaction();
    }

    private void EnsureConnectionIsOpen()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
    }


    public ISqlContainer CreateSqlContainer(string? query = null)
    {
        return new SqlContainer(this, _context.TypeMapRegistry, query);
    }

    public DbParameter CreateDbParameter<T>(string name, DbType type, T value)
    {
       return _context.CreateDbParameter(name, type, value);
    }

    public DbConnection GetConnection(ExecutionType type) => _connection;
    public string WrapObjectName(string name)
    {
        return _context.WrapObjectName(name);
    }

    public TransactionContext BeginTransaction()
    {
        throw new NotImplementedException("TransactionContext cannot start a new transaction, nested transactions are not supported.");
    }

    public DbMode ConnectionMode => DbMode.SingleConnection;

    public ITypeMapRegistry TypeMapRegistry => _context.TypeMapRegistry;

    public IDataSourceInformation DataSourceInfo => _context.DataSourceInfo;

    public void Commit()
    {
        ThrowIfDisposed();
        if (_committed) return;

        _transaction.Commit();
        _committed = true;
    }

    public void Rollback()
    {
        ThrowIfDisposed();
        if (_committed) return;

        _transaction.Rollback();
        _transaction.Dispose();
       
        
        _committed = true;
    }

    private void ThrowIfDisposed()
    {
        if (Interlocked.Read(ref _disposed)==1)
            throw new ObjectDisposedException(nameof(TransactionContext));
    }

    private void Dispose(bool disposing)
    {
        if (Interlocked.Increment(ref _disposed) == 1)
        {
            if (!_committed)
            {
                Rollback();
            }
            if(_context.ConnectionMode == DbMode.Standard){
                _connection.Dispose();
            }
        
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

    
    }

    public void Dispose()
    {
        Dispose(true);
     
    }
}
