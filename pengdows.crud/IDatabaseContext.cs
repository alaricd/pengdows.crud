using System.Data;
using System.Data.Common;

namespace pengdows.crud;

public interface IDatabaseContext : IDisposable
{
     DbMode ConnectionMode { get; }
    ITypeMapRegistry TypeMapRegistry { get; }
    IDataSourceInformation DataSourceInfo { get; }
    string SessionSettingsPreamble { get; }
    ProcWrappingStyle ProcWrappingStyle { get; }
    int MaxParameterLimit { get; }
    long NumberOfOpenConnections { get; }

    string QuotePrefix
    {
        get;
    }

    string QuoteSuffix { get; }
    string CompositeIdentifierSeparator { get; }
    ISqlContainer CreateSqlContainer(string? query = null);
    DbParameter CreateDbParameter<T>(string name, DbType type, T value);
    DbConnection GetConnection(ExecutionType executionType);
    string WrapObjectName(string name);
    TransactionContext BeginTransaction(IsolationLevel? isolationLevel = null);
    string GenerateRandomName(int length = 8);
    DbParameter CreateDbParameter<T>(DbType type, T value);

    void AssertIsReadConnection();
    void AssertIsWriteConnection();
    void CloseAndDisposeConnection(DbConnection connection);
    string MakeParameterName(DbParameter dbParameter);
}