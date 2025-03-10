using System.Data;
using System.Data.Common;

namespace pengdows.crud;

public interface IDbContext : IDisposable
{
    DataSourceInformation DataSourceInfo { get; }
    DbMode ConnectionMode { get;   }
    DbConnection Connection { get; }
    SqlContainer CreateSqlContainer();
    DbConnection GetConnection(ExecutionType type);
    void AddStateChangeHandler(DbConnection connection);
    void ApplyIndexedViewSettings(DbConnection connection);
    string WrapObjectName(string name);
    DbParameter CreateDbParameter<T>(string name, DbType type, T value);
}