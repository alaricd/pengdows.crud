using System.Data;
using System.Data.Common;
using pengdows.crud.pengdows.crud;

namespace pengdows.crud;

public interface IDbContext : IDisposable
{
    DataSourceInformation DataSourceInfo { get; }
    DbMode ConnectionMode { get;   }
    DbConnection Connection { get; }
    SqlContainer CreateSqlContainer();
    DbConnection GetConnection(ExecutionType type);
    DbParameter CreateDbParameter<T>(string name, DbType type, T value);
    void AddStateChangeHandler(DbConnection connection);
    void ApplyIndexedViewSettings(DbConnection connection);
    string WrapObjectName(string name);
}