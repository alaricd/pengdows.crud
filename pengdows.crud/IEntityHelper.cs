#region

using System.Data.Common;
using System.Reflection;
using pengdows.crud.enums;
using pengdows.crud.wrappers;

#endregion

namespace pengdows.crud;

public interface IEntityHelper<TEntity, TRowID> where TEntity : class, new()
{
    string WrappedTableName { get; }
    public EnumParseFailureMode EnumParseBehavior { get; set; }
    ISqlContainer BuildCreate(TEntity objectToCreate, IDatabaseContext? context = null);
    ISqlContainer BuildBaseRetrieve(string alias, IDatabaseContext? context = null);
    ISqlContainer BuildRetrieve(IReadOnlyCollection<TRowID>? listOfIds = null, string alias = "a", IDatabaseContext? context = null);
    ISqlContainer BuildRetrieve(IReadOnlyCollection<TEntity>? listOfObjects = null, string alias = "a", IDatabaseContext? context = null);
    ISqlContainer BuildRetrieve(IReadOnlyCollection<TRowID>? listOfIds = null, IDatabaseContext? context = null);
    ISqlContainer BuildRetrieve(IReadOnlyCollection<TEntity>? listOfObjects = null, IDatabaseContext? context = null);

    Task<ISqlContainer> BuildUpdateAsync(TEntity objectToUpdate, IDatabaseContext? context = null);

    Task<ISqlContainer> BuildUpdateAsync(TEntity objectToUpdate, bool loadOriginal,
        IDatabaseContext? context = null);

    ISqlContainer BuildDelete(TRowID id, IDatabaseContext? context = null);
    public Task<TEntity?> RetrieveOneAsync(TEntity objectToRetrieve, IDatabaseContext? context = null);
    public Task<TEntity?> LoadSingleAsync(ISqlContainer sc);
    public Task<List<TEntity>> LoadListAsync(ISqlContainer sc);
    string MakeParameterName(DbParameter p);
    Action<object, object?> GetOrCreateSetter(PropertyInfo prop);
    TEntity MapReaderToObject(ITrackedReader reader);
    ISqlContainer BuildWhere(string wrappedColumnName, IEnumerable<TRowID> ids, ISqlContainer sqlContainer);
    public void BuildWhereByPrimaryKey(IReadOnlyCollection<TEntity>? listOfObjects, ISqlContainer sc, string alias = "a");
}