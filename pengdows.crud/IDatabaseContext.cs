#region

using System.Data;
using System.Data.Common;
using pengdows.crud.wrappers;

#endregion

namespace pengdows.crud;

public interface IDatabaseContext : IDisposable
{
    DbMode ConnectionMode { get; }
    ITypeMapRegistry TypeMapRegistry { get; }
    IDataSourceInformation DataSourceInfo { get; }
    string SessionSettingsPreamble { get; }
    ProcWrappingStyle ProcWrappingStyle { get; set; }
    int MaxParameterLimit { get; }
    long NumberOfOpenConnections { get; }

    string QuotePrefix { get; }

    string QuoteSuffix { get; }
    string CompositeIdentifierSeparator { get; }

    SupportedDatabase Product { get; }
    long MaxNumberOfConnections { get; }

    ISqlContainer CreateSqlContainer(string? query = null);
    DbParameter CreateDbParameter<T>(string name, DbType type, T value);
    ITrackedConnection GetConnection(ExecutionType executionType);
    string WrapObjectName(string name);
    TransactionContext BeginTransaction(IsolationLevel? isolationLevel = null);
    string GenerateRandomName(int length = 8);
    DbParameter CreateDbParameter<T>(DbType type, T value);

    void AssertIsReadConnection();
    void AssertIsWriteConnection();
    
   // void CloseAndDisposeConnection(DbConnection connection);
   string MakeParameterName(DbParameter dbParameter);
   string MakeParameterName(string parameterName);
    void CloseAndDisposeConnection(ITrackedConnection? conn);
}