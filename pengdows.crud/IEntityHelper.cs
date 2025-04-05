using System.Data.Common;
using System.Reflection;
using pengdows.crud.enums;

namespace pengdows.crud;

public interface IEntityHelper<T, TID> where T : class, new()
{
    string WrappedTableName { get; }
    public EnumParseFailureMode EnumParseBehavior { get; set; }
    ISqlContainer BuildCreate(T objectToCreate, IDatabaseContext? context = null);
    ISqlContainer BuildBaseRetrieve(string alias, IDatabaseContext? context = null);
    ISqlContainer BuildRetrieve(List<TID>? listOfIds = null, IDatabaseContext? context = null, string alias = "a");
    ISqlContainer BuildRetrieve(List<T>? listOfObjects = null, IDatabaseContext? context = null, string alias = "a");

    Task<ISqlContainer> BuildUpdateAsync(T objectToUpdate, IDatabaseContext? context = null);

    Task<ISqlContainer> BuildUpdateAsync(T objectToUpdate, bool loadOriginal = true,
        IDatabaseContext? context = null);

    ISqlContainer BuildDelete(TID id, IDatabaseContext? context = null);
    public Task<T?> RetrieveOneAsync(T objectToUpdate, IDatabaseContext? context = null);
    public Task<T?> LoadSingleAsync(ISqlContainer sc);
    public Task<List<T>> LoadListAsync(ISqlContainer sc);
    string MakeParameterName(DbParameter p);
    Action<object, object?> GetOrCreateSetter(PropertyInfo prop);
    T MapReaderToObject(DbDataReader reader);
  
    ISqlContainer BuildWhere(string wrappedColumnName, IEnumerable<TID> ids, ISqlContainer sqlContainer,
        IDatabaseContext context);

   // void BuildWhereByPrimaryKey(string wrappedColumnName, List<T>? listOfObjects, ISqlContainer sc, IDatabaseContext context);
}