using System.Data;
using System.Data.Common;

namespace pengdows.crud;

public interface IDatabaseContext : IDisposable
{
    DbMode ConnectionMode { get; }
    ITypeMapRegistry TypeMapRegistry { get; }
    IDataSourceInformation DataSourceInfo { get; }
    string MissingSqlSettings { get; }
    ISqlContainer CreateSqlContainer(string? query = null);
    DbParameter CreateDbParameter<T>(string name, DbType type, T value);
    DbConnection GetConnection(ExecutionType executionType);
    string WrapObjectName(string name);
    TransactionContext BeginTransaction();
    string GenerateRandomName(int length = 8);
}