using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;
using System.Text;
using System.Linq.Expressions;

namespace pengdows.crud
{
    public class EntityHelper<T, TID> : ISqlBuilder<T, TID> where T:new()
    {
        readonly IDatabaseContext _context;
        readonly ITypeMapRegistry _typeMap;
        readonly TableInfo _tableInfo;
        readonly string _parameterMarker;
        readonly bool _hasNamedParameters;
        private readonly string _tableName;
        private readonly ColumnInfo? _idColumn;

        private readonly ColumnInfo? _versionColumn;

        // Cache for compiled property setters
        private static readonly ConcurrentDictionary<PropertyInfo, Action<object, object?>> _propertySetters = new();


        public EntityHelper(IDatabaseContext databaseContext, ITypeMapRegistry typeMap)
        {
            _context = databaseContext;
            _typeMap = typeMap;
            _tableInfo = _typeMap.GetTableInfo<T>() ??
                         throw new InvalidOperationException($"Type {typeof(T).FullName} is not a table.");
            _parameterMarker = _context.DataSourceInfo.ParameterMarker;
            _hasNamedParameters = _context.DataSourceInfo.ParameterMarker == "?";

            _tableName = (!string.IsNullOrEmpty(_tableInfo.Schema)
                             ? _context.WrapObjectName(_tableInfo.Schema) + "."
                             : "")
                         + _context.WrapObjectName(_tableInfo.Name);
            _idColumn = _tableInfo.Columns.Values.FirstOrDefault(itm => itm.IsId);
            _versionColumn = _tableInfo.Columns.Values.FirstOrDefault(itm => itm.IsVersion);
        }

        public string MakeParameterName(DbParameter p) =>
            _hasNamedParameters ? "?" : $"{_parameterMarker}{p.ParameterName}";

 
        private Action<object, object?> GetOrCreateSetter(PropertyInfo prop)
        {
            return _propertySetters.GetOrAdd(prop, p =>
            {
                var objParam = Expression.Parameter(typeof(object));
                var valueParam = Expression.Parameter(typeof(object));

                var castObj = Expression.Convert(objParam, p.DeclaringType!);
                var castValue = Expression.Convert(valueParam, p.PropertyType);

                var propertyAccess = Expression.Property(castObj, p);
                var assignment = Expression.Assign(propertyAccess, castValue);

                var lambda = Expression.Lambda<Action<object, object?>>(assignment, objParam, valueParam);
                return lambda.Compile();
            });
        }

        private T MapReaderToObject<T>(DbDataReader reader) where T : new()
        {
            var obj = new T();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var colName = reader.GetName(i);
                if (_tableInfo.Columns.TryGetValue(colName, out var column))
                {
                    var value = reader.GetValue(i);
                    if (value != DBNull.Value)
                    {
                        var setter = GetOrCreateSetter(column.PropertyInfo);
                        setter(obj, value);
                    }
                }
            }

            return obj;
        }

        public async Task<T?> LoadSingleAsync<T>(ISqlContainer sc) where T : new()
        {
            await using var reader = await sc.ExecuteReaderAsync().ConfigureAwait(false);
            return await reader.ReadAsync().ConfigureAwait(false) ? MapReaderToObject<T>(reader) : default;
        }

        public async Task<List<T>> LoadListAsync<T>(ISqlContainer sc) where T : new()
        {
            var list = new List<T>();
            await using var reader = await sc.ExecuteReaderAsync().ConfigureAwait(false);

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(MapReaderToObject<T>(reader));
            }

            return list;
        }

       public ISqlContainer BuildCreate(T objectToCreate, IDatabaseContext? context = null)
        {
            if (objectToCreate == null)
                throw new ArgumentNullException(nameof(objectToCreate));

            context ??= _context;
            var columns = new StringBuilder();
            var values = new StringBuilder();
            var parameters = new List<DbParameter>();
            var pid = 0;
            var sc = new SqlContainer(context, _typeMap);

            foreach (var column in _tableInfo.Columns.Values)
            {
                if (columns.Length > 0)
                {
                    columns.Append(", ");
                    values.Append(", ");
                }

                var paramName = $"p{pid++}";
                var p = _context.CreateDbParameter(paramName,
                    column.DbType,
                    column.PropertyInfo.GetValue(objectToCreate) ?? DBNull.Value);

                columns.Append(_context.WrapObjectName(column.Name));
                values.Append(MakeParameterName(p));
                parameters.Add(p);
            }

            sc.Query.Append("INSERT INTO ")
                .Append(_tableName)
                .Append(" (")
                .Append(columns)
                .Append(") VALUES (")
                .Append(values)
                .Append(")");

            sc.AppendParameters(parameters);
            return sc;
        }

        public ISqlContainer BuildRetrieve(List<TID>? objectsToRetrieve = null, IDatabaseContext? context = null)
        {
            context ??= _context;
            var sc = new SqlContainer(context, _typeMap);
            var sb = sc.Query;
            sb.Append("SELECT ");
            sb.Append(string.Join(", ", _tableInfo.Columns.Values.Select(col => _context.WrapObjectName(col.Name))));
            sb.Append(" FROM ").Append(_tableName);

            var parameters = new List<DbParameter>();

            if (objectsToRetrieve?.Count > 0 && _idColumn != null)
            {
                sb.Append(" WHERE ").Append(context.WrapObjectName(_idColumn.Name)).Append(" IN (");

                for (var i = 0; i < objectsToRetrieve.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    var p = context.CreateDbParameter($"p{i}", _idColumn.DbType, objectsToRetrieve[i]);
                    sb.Append(MakeParameterName(p));
                    parameters.Add(p);
                }

                sb.Append(")");
            }

            sc.AppendParameters(parameters);
            return sc;
        }

        public ISqlContainer BuildUpdate(T objectToUpdate, IDatabaseContext? context)
        {
            if (objectToUpdate == null)
                throw new ArgumentNullException(nameof(objectToUpdate));
            context ??= _context;
            var setClause = new StringBuilder();
            var parameters = new List<DbParameter>();
            var pid = 0;
            var sc = new SqlContainer(context, _typeMap);

            var original = RetrieveOneAsync(objectToUpdate);

            foreach (var column in _tableInfo.Columns.Values)
            {
                if (column.IsId)
                {
                    continue;
                }

                if (column.IsVersion)
                {
                    continue;
                }

                if (setClause.Length > 0) setClause.Append(", ");

                var paramName = $"p{pid++}";
                var param = context.CreateDbParameter(paramName,
                    column.DbType,
                    column.PropertyInfo.GetValue(objectToUpdate) ?? DBNull.Value);
                parameters.Add(param);

                setClause.Append($"{context.WrapObjectName(column.Name)} = {MakeParameterName(param)}");
            }

            if (_idColumn == null)
                throw new InvalidOperationException("Table does not have a primary key.");

            var pId = context.CreateDbParameter(
                "pId",
                _idColumn.DbType,
                _idColumn.PropertyInfo.GetValue(objectToUpdate) ?? DBNull.Value);
            parameters.Add(pId);

            sc.Query.Append("UPDATE ")
                .Append(_tableName)
                .Append(" SET ")
                .Append(setClause)
                .Append(" WHERE ")
                .Append(context.WrapObjectName(_idColumn.Name))
                .Append($" = {MakeParameterName(pId)}");

            if (_versionColumn != null)
            {
                var pVersion = context.CreateDbParameter("pVersion",
                    _versionColumn.DbType,
                    _versionColumn.PropertyInfo.GetValue(objectToUpdate) ?? DBNull.Value);
                sc.Query.Append(" AND ")
                    .Append(context.WrapObjectName(_versionColumn.Name))
                    .Append($" = {MakeParameterName(pVersion)}");
                parameters.Add(pVersion);
            }

            sc.AppendParameters(parameters);
            return sc;
        }


        public Task<T?> RetrieveOneAsync(T objectToUpdate, IDatabaseContext? context = null)
        {
            context ??= _context;
            var id = (TID)_idColumn.PropertyInfo.GetValue(objectToUpdate);
            var sc = BuildRetrieve([id], context);
            return LoadSingleAsync<T>(sc);
        }

        public ISqlContainer BuildDelete(TID id, IDatabaseContext? context = null)
        {
            context ??= _context;
            var sc = new SqlContainer(context, _typeMap);

            var idCol = _idColumn;
            if (idCol == null)
            {
                throw new InvalidOperationException($"row identity column for table {_tableName} not found");
            }

            var p = _context.CreateDbParameter("id", idCol.DbType, id);
            sc.AppendParameters(p);

            sc.Query.Append("DELETE FROM ")
                .Append(_tableName)
                .Append(" WHERE ")
                .Append(context.WrapObjectName(idCol.Name))
                .Append(" = ")
                .Append(MakeParameterName(p));

            return sc;
        }
    }

    public interface ISqlBuilder<T, TID> where T : new()
    {
        string MakeParameterName(DbParameter p);
        ISqlContainer BuildCreate(T objectToCreate, IDatabaseContext? context = null);
        ISqlContainer BuildRetrieve(List<TID>? objectsToRetrieve = null, IDatabaseContext? context = null);
        ISqlContainer BuildUpdate(T objectToUpdate, IDatabaseContext? context = null);
        ISqlContainer BuildDelete(TID id, IDatabaseContext? context = null);
        public Task<T?> RetrieveOneAsync(T objectToUpdate, IDatabaseContext? context = null);
        public Task<T?> LoadSingleAsync<T>(ISqlContainer sc) where T : new();
        public Task<List<T>> LoadListAsync<T>(ISqlContainer sc) where T : new();
    }
}