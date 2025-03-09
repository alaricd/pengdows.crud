using System.Data;
using System.Data.Common;
using System.Text;

namespace pengdows.crud;

public abstract class DbContext : IDbContext
{
    private int _disposeCount;
    protected internal DbProviderFactory Factory { get; set; }
    protected internal bool IsSqlServer { get; set; }
    protected internal string ConnectionString { get; set; }
    protected int _connectionCount { get; }
    protected internal DbMode ConnectionMode { get; set; }
    protected internal DbConnection Connection { get; set; }

    public DataSourceInformation DataSourceInfo { get; set; }

    // Standard – uses connection pooling, asking for a new connection each time a statement is executed, unless a transaction is being used.
    //     SingleConnection – funnels everything through a single connection, useful for databases that only allow a single connection.
    //     SqlCe – keeps a single connection open all the time, using it for all write access, while allowing many read-only connections. This prevents the database being unloaded and keeping within the rule of only having a single write connection open.
    //     SqlExpressUserMode – The same as “Standard”, however it keeps 1 connection open to prevent unloading of the database. This is useful for the new localDb feature in SQL Express.
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
                {
                    Interlocked.Increment(ref _connectionCount);
                    if (IsSqlServer) ApplyIndexedViewSettings(connection);

                    break;
                }
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
        if (string.IsNullOrEmpty(name)) return name;
        var ss = name.Split(DataSourceInfo.SchemaSeparator);
        for (var i = 0; i < ss.Length; i++) ss[i] = DataSourceInfo.QuotePrefix + ss[i] + DataSourceInfo.QuoteSuffix;
        return ss.Join(DataSourceInfo.SchemaSeparator);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    public abstract DbConnection GetConnection(ExecutionType write);

    public SqlContainer BuildRetrieveSql<T>(string? aliasArg = null)
    {
        var tableInfo = TypeMapRegistry.GetTableInfo<T>();
        var alias = !string.IsNullOrEmpty(aliasArg) ? aliasArg : "a";
        var sb = new StringBuilder();

        foreach (var column in tableInfo.Columns.Values)
        {
            if (sb.Length > 0) sb.Append(", \n");
            sb.Append(WrapObjectName(alias))
                .Append(DataSourceInfo.SchemaSeparator)
                .Append(WrapObjectName(column.Name));
        }

        if (sb.Length == 0) throw new InvalidOperationException("No columns found for table.");

        sb.Insert(0, "SELECT ");
        sb.Append("\nFROM ")
            .Append(WrapObjectName(tableInfo.Schema))
            .Append(DataSourceInfo.SchemaSeparator)
            .Append(WrapObjectName(tableInfo.Name));

        return new SqlContainer(this, sb.ToString());
    }

    public abstract TransactionContext BeginTransaction();

    public void Dispose(bool isDisposing)
    {
        if (Interlocked.Increment(ref _disposeCount) == 1)
        {
            Connection?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    ~DbContext()
    {
        Dispose(false);
    }
}