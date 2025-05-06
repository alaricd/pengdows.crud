#region

using System.Data;
using System.Data.Common;
using pengdows.crud.enums;
using pengdows.crud.infrastructure;
using pengdows.crud.threading;
using pengdows.crud.wrappers;

#endregion

namespace pengdows.crud;

public interface IDatabaseContext : ISafeAsyncDisposableBase
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

    string DatabaseProductName => DataSourceInfo.DatabaseProductName;

    bool PrepareStatements => DataSourceInfo.PrepareStatements;
    bool SupportsNamedParameters => DataSourceInfo.SupportsNamedParameters;

    bool IsReadOnlyConnection { get; }

    ILockerAsync GetLock();

    ISqlContainer CreateSqlContainer(string? query = null);
    DbParameter CreateDbParameter<T>(string? name, DbType type, T value);
    ITrackedConnection GetConnection(ExecutionType executionType, bool isShared = false);
    string WrapObjectName(string name);
    ITransactionContext BeginTransaction(IsolationLevel? isolationLevel = null);
    string GenerateRandomName(int length = 5, int parameterNameMaxLength = 30);
    DbParameter CreateDbParameter<T>(DbType type, T value);

    void AssertIsReadConnection();
    void AssertIsWriteConnection();

    // void CloseAndDisposeConnection(DbConnection connection);
    string MakeParameterName(DbParameter dbParameter);
    string MakeParameterName(string parameterName);
    void CloseAndDisposeConnection(ITrackedConnection? conn);
}