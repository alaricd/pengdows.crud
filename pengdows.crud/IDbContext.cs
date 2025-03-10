using System.Data;
using System.Data.Common;

namespace pengdows.crud;

public interface IDbContext : IDisposable
{
    IDataSourceInformation DataSourceInfo { get; }
    ISqlContainer CreateSqlContainer(string query = "");
    DbParameter CreateDbParameter<T>(string name, DbType type, T value);
    DbConnection GetConnection(ExecutionType executionType);   DbMode ConnectionMode { get;   }
    DbConnection Connection { get; }
    void AddStateChangeHandler(DbConnection connection);
    void ApplyIndexedViewSettings(DbConnection connection);
    string WrapObjectName(string name);
    
}
