using System.Data.Common;

namespace pengdows.crud;

public abstract class OptimizedQueryLoader<T, ID> : IOptimizedQueryLoader<T, ID>
{
    public abstract ISqlContainer BuildRetrieve(IDatabaseContext context, ID id);
    public abstract ISqlContainer BuildInsert(IDatabaseContext context, T obj);
    public abstract ISqlContainer BuildUpdate(IDatabaseContext context, T obj);
    public abstract T MapFromReader(DbDataReader reader);

    public virtual async IAsyncEnumerable<T> ExecuteCustomRetrieve(IDatabaseContext context, ISqlContainer sqlContainer,
        Func<DbDataReader, T> mapFunc)
    {
        var container = sqlContainer ?? throw new ArgumentNullException(nameof(sqlContainer));
        await using var reader = container.ExecuteReaderAsync().Result;
        while (await reader.ReadAsync()) yield return mapFunc(reader);
    }
}