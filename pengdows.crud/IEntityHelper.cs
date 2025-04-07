using System.Data.Common;
using System.Reflection;
using pengdows.crud.enums;

namespace pengdows.crud;

public interface IEntityHelper<T, TID> where T : class, new()
{
    string WrappedTableName { get; }
    public EnumParseFailureMode EnumParseBehavior { get; set; }
    ISqlContainer BuildCreate(T objectToCreate);
    ISqlContainer BuildBaseRetrieve(string alias);
    ISqlContainer BuildRetrieve(List<TID>? listOfIds = null, string alias = "a");
    ISqlContainer BuildRetrieve(List<T>? listOfObjects = null, string alias = "a");

    Task<ISqlContainer> BuildUpdateAsync(T objectToUpdate);

    Task<ISqlContainer> BuildUpdateAsync(T objectToUpdate, bool loadOriginal = true,
        IDatabaseContext? context = null);

    ISqlContainer BuildDelete(TID id);
    public Task<T?> RetrieveOneAsync(T objectToUpdate);
    public Task<T?> LoadSingleAsync(ISqlContainer sc);
    public Task<List<T>> LoadListAsync(ISqlContainer sc);
    string MakeParameterName(DbParameter p);
    Action<object, object?> GetOrCreateSetter(PropertyInfo prop);
    T MapReaderToObject(DbDataReader reader);
    ISqlContainer BuildWhere(string wrappedColumnName, IEnumerable<TID> ids, ISqlContainer sqlContainer);
    public void BuildWhereByPrimaryKey(List<T>? listOfObjects, ISqlContainer sc, string alias = "a");

}