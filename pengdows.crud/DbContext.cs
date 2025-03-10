using System.Data;
using System.Data.Common;
using System.Text;

namespace pengdows.crud;

public abstract class DbContext : IDbContext
{
    private int _disposeCount;
    protected DbProviderFactory Factory { get; set; }
    protected bool IsSqlServer { get; set; }
    protected string ConnectionString { get; set; }
    protected int _connectionCount;
    public DbMode ConnectionMode { get; set; }
    public DbConnection Connection { get; set; }

    public DataSourceInformation DataSourceInfo { get; set; }

    public void AddStateChangeHandler(DbConnection connection)
    {
        connection.StateChange += (sender, args) =>
        {
            switch (args.CurrentState)
            {
                case ConnectionState.Closed:
                    Interlocked.Decrement(ref _connectionCount);
                    break;
                case ConnectionState.Open:
                    Interlocked.Increment(ref _connectionCount);
                    if (IsSqlServer)
                    {
                        ApplyIndexedViewSettings(connection);
                    }

                    break;
            }
        };
    }

    public void ApplyIndexedViewSettings(DbConnection connection)
    {
        if (IsSqlServer)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SET ANSI_NULLS ON;
                SET ANSI_PADDING ON;
                SET ANSI_WARNINGS ON;
                SET ARITHABORT ON;
                SET CONCAT_NULL_YIELDS_NULL ON;
                SET QUOTED_IDENTIFIER ON;
                SET NUMERIC_ROUNDABORT OFF;";
            cmd.ExecuteNonQuery();
        }
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

    public DbParameter CreateDbParameter<T>(string name, DbType type, T value)
    {
        var p = this.Factory.CreateParameter() ?? throw new InvalidOperationException("Failed to create parameter.");
        p.ParameterName = name;
        p.DbType = type;
        p.Value = value;
        return p;
    }

    public abstract DbConnection GetConnection(ExecutionType type);

 
    public SqlContainer CreateSqlContainer() => new(this);

    public abstract TransactionContext BeginTransaction();

    public void Dispose()
    {
        Dispose(true);
    }

    protected void Dispose(bool disposing, Action? action = null)
    {
        if (Interlocked.Increment(ref _disposeCount) == 1)
        {
            if (disposing)
            {
                if (action != null)
                {
                    action();
                }

                Connection?.Dispose(); 
                GC.SuppressFinalize(this);
            }

           
        }
    }
    

    ~DbContext()
    {
        Dispose(false);
    }
}
