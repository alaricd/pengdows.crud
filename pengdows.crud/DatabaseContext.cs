using System.Data.Common;

namespace pengdows.crud;

public class DatabaseContext : DbContext
{
    public DatabaseContext(string connectionString, string providerName, DbMode mode = DbMode.Standard)
    {
        ConnectionMode = mode;
        IsSqlServer = providerName.Contains("sqlclient", StringComparison.OrdinalIgnoreCase);
        Factory = DbProviderFactories.GetFactory(providerName);
        var csb = Factory.CreateConnectionStringBuilder() ?? new DbConnectionStringBuilder();
        DbConnection conn = null;
        try
        {
            csb.ConnectionString = connectionString;
            conn.ConnectionString = csb.ConnectionString;

            conn.Open();
            ApplyIndexedViewSettings(conn);
            Interlocked.Increment(ref _connectionCount);
            ConnectionString = csb.ConnectionString;
            DataSourceInfo = new DataSourceInformation(conn);
            if (mode != DbMode.Standard)
            {
                Connection = conn;
            }
        }
        finally
        {
            if (mode != DbMode.Standard)
            {
                Interlocked.Decrement(ref _connectionCount);
                conn?.Dispose();
            }
        }
    }



    public override TransactionContext BeginTransaction()
    {
        return new TransactionContext(this);
    }



    private DbConnection GetStandardConnection()
    {
        var connection = Factory.CreateConnection();
        connection.ConnectionString = ConnectionString;
        AddStateChangeHandler(connection);
        return connection;
    }

    private DbConnection GetSingleConnection(ExecutionType type)
    {
        return Connection;
    }

    private DbConnection GetSqlCeConnection(ExecutionType type)
    {
        if (ExecutionType.Read == type)
        {
            return GetStandardConnection();
        }

        return Connection;
    }

    private DbConnection GetSqlExpressUserModeConnection(ExecutionType type)
    {
        //Just return a new connection, leaving the 1 connection open;
        return GetStandardConnection();
    }

    public override DbConnection GetConnection(ExecutionType type = ExecutionType.Read)
    {
        switch (ConnectionMode)
        {
            case DbMode.Standard:
                return GetStandardConnection();
            case DbMode.SqlExpressUserMode:
                return GetSqlExpressUserModeConnection(ExecutionType.Write);
            case DbMode.SqlCe:
                return GetSqlCeConnection(ExecutionType.Write);
            case DbMode.SingleConnection:
                return GetSingleConnection(ExecutionType.Write);
            default:
                throw new InvalidOperationException("Invalid connection mode.");
        }
    }

}