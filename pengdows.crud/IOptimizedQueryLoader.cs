using System.Data.Common;

namespace pengdows.crud;

public interface IOptimizedQueryLoader<T, ID>
{
    public  ISqlContainer BuildRetrieve(IDatabaseContext context, ID id);
    public  ISqlContainer BuildInsert(IDatabaseContext context, T obj);
    public  ISqlContainer BuildUpdate(IDatabaseContext context, T obj);
    public  T MapFromReader(DbDataReader reader);

    public IAsyncEnumerable<T> ExecuteCustomRetrieve(IDatabaseContext context, ISqlContainer sqlContainer,
        Func<DbDataReader, T> mapFunc);
}