using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace pengdows.crud;

public class EntityHelper<T, TID> : IEntityHelper<T, TID> where T : class, new()
{
    // Cache for compiled property setters
    private static readonly ConcurrentDictionary<PropertyInfo, Action<object, object?>> _propertySetters = new();
    private readonly IDatabaseContext _context;
    private readonly ColumnInfo? _idColumn;
    private readonly string _parameterMarker;
    private readonly TableInfo _tableInfo;
    private readonly string _tableName;
    private readonly ITypeMapRegistry _typeMap;
    private readonly bool _usePositionalParameters;

    private readonly ColumnInfo? _versionColumn;

    public EntityHelper(IDatabaseContext databaseContext) :
        this(databaseContext, databaseContext.TypeMapRegistry)
    {
    }

    public EntityHelper(IDatabaseContext databaseContext, ITypeMapRegistry typeMap)
    {
        _context = databaseContext;
        _typeMap = typeMap;
        _tableInfo = _typeMap.GetTableInfo<T>() ??
                     throw new InvalidOperationException($"Type {typeof(T).FullName} is not a table.");
        _parameterMarker = _context.DataSourceInfo.ParameterMarker;
        _usePositionalParameters = _context.DataSourceInfo.ParameterMarker == "?";

        _tableName = (!string.IsNullOrEmpty(_tableInfo.Schema)
                         ? _context.WrapObjectName(_tableInfo.Schema) + _context.DataSourceInfo.CompositeIdentifierSeparator
                         : "")
                     + _context.WrapObjectName(_tableInfo.Name);
        _idColumn = _tableInfo.Columns.Values.FirstOrDefault(itm => itm.IsId);
        _versionColumn = _tableInfo.Columns.Values.FirstOrDefault(itm => itm.IsVersion);
    }

    public String WrappedTableName
    {
        get
        {
            return _tableName;
        }
    }

    public string MakeParameterName(DbParameter p)
    {
        return _usePositionalParameters ? "?" : $"{_parameterMarker}{p.ParameterName}";
    }


    public Action<object, object?> GetOrCreateSetter(PropertyInfo prop)
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

    public T MapReaderToObject(DbDataReader reader)
    {
        var obj = new T();

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var colName = reader.GetName(i);
            if (_tableInfo.Columns.TryGetValue(colName, out var column))
            {
                LogReader(reader);

                object? value;
                var rawValue = reader.GetValue(i);
                if (rawValue == DBNull.Value || rawValue == null)
                {
                    value = null;
                }
                else
                {
                    var typeName = rawValue.GetType().FullName ?? "";
                    if (typeName.Contains("MySqlDateTime")) // MySql.Data.Types.MySqlDateTime mysqlDate)
                    {
                        //value = mysqlDate.IsNull || !mysqlDate.IsValidDateTime ? null : mysqlDate.GetDateTime();
                        value = rawValue;
                    }
                    else
                    {
                        value = rawValue;
                    }
                }

                Console.WriteLine($"{colName}:{value}");

                if (value != null && value != DBNull.Value)
                {
                    var setter = GetOrCreateSetter(column.PropertyInfo);
                    setter(obj, value);
                }
            }
        }

        return obj;
    }

    private static void LogReader(DbDataReader reader)
    {
        Console.WriteLine($"Reader FieldCount: {reader.FieldCount}");
        for (int i = 0; i < reader?.FieldCount; i++)
        {
            var t = reader.GetFieldType(i);
            Console.WriteLine($"Column {i}: {reader.GetName(i)} ({t})");
            if (t == typeof(DateTime))
            {
                Console.WriteLine(reader?.GetFieldValue<DateTime?>(i)?.ToString("yyyy-MM-dd HH:mm:ss"));
            }
        }
    }

    public Task<T?> RetrieveOneAsync(T objectToUpdate, IDatabaseContext? context = null)
    {
        context ??= _context;
        var id = (TID)_idColumn.PropertyInfo.GetValue(objectToUpdate);
        var sc = BuildRetrieve([id], context);
        return LoadSingleAsync(sc);
    }

    public async Task<T?> LoadSingleAsync(ISqlContainer sc)
    {
        await using var reader = await sc.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            return MapReaderToObject(reader);
        }

        return null;
    }

    public async Task<List<T>> LoadListAsync(ISqlContainer sc)
    {
        var list = new List<T>();
        var reader = await sc.ExecuteReaderAsync().ConfigureAwait(false);

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(MapReaderToObject(reader));
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
        var sc = new SqlContainer(context);

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
                column.PropertyInfo.GetValue(objectToCreate));

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
        //Console.WriteLine(sc.Query); // Should show @p0, @p1, etc.
        //foreach (var param in parameters)
        //    Console.WriteLine($"[{param.ParameterName}] = {param.Value} ({param.DbType})");
        return sc;
    }

    public ISqlContainer BuildRetrieve(List<TID>? listOfIds = null, IDatabaseContext? context = null)
    {
        context ??= _context;
        var sc = new SqlContainer(context);
        var sb = sc.Query;
        sb.Append("SELECT ");
        sb.Append(string.Join(", ", _tableInfo.Columns.Values.Select(col => _context.WrapObjectName(col.Name))));
        sb.Append(" FROM ").Append(_tableName);

        BuildWhere(_context.WrapObjectName(_idColumn.Name), listOfIds, sc, context);

        return sc;
    }

    public Task<ISqlContainer> BuildUpdateAsync(T objectToUpdate, IDatabaseContext? context = null)
    {
        return BuildUpdateAsync(objectToUpdate, _versionColumn != null, context);
    }

    public async Task<ISqlContainer> BuildUpdateAsync(T objectToUpdate, bool loadOriginal = true,
        IDatabaseContext? context = null)
    {
        if (objectToUpdate == null)
            throw new ArgumentNullException(nameof(objectToUpdate));

        context ??= _context;
        var setClause = new StringBuilder();
        var parameters = new List<DbParameter>();
        var sc = new SqlContainer(context);
        var original = null as T;

        if (loadOriginal)
        {
            original = await RetrieveOneAsync(objectToUpdate, context);
            if (original == null)
                throw new InvalidOperationException("Original record not found for update.");
        }

        foreach (var column in _tableInfo.Columns.Values)
        {
            if (column.IsId || column.IsVersion || column.IsNonUpdateable)
            {
                //Skip columns that should never be directly updated
                //the version column, rowId and columns we have marked 
                //as non-updateable
                continue;
            }

            var newValue = column.PropertyInfo.GetValue(objectToUpdate);
            var originalValue = loadOriginal ? column.PropertyInfo.GetValue(original) : null;

            // Skip unchanged values if original is loaded.
            if (loadOriginal && Equals(newValue, originalValue)) continue;

            if (setClause.Length > 0) setClause.Append(", ");

            if (newValue == null)
            {
                setClause.Append($"{context.WrapObjectName(column.Name)} = NULL");
            }
            else
            {
                var paramName = $"p{parameters.Count}";
                var param = context.CreateDbParameter(paramName, column.DbType, newValue);
                parameters.Add(param);
                setClause.Append($"{context.WrapObjectName(column.Name)} = {MakeParameterName(param)}");
            }
        }

        if (_versionColumn != null)
        {
            //this should be updated to wrap other patterns.
            setClause.Append(
                $", {_context.WrapObjectName(_versionColumn.Name)} = {_context.WrapObjectName(_versionColumn.Name)} + 1");
        }
    
        if (setClause.Length == 0)
            throw new InvalidOperationException("No changes detected for update.");

        var pId = context.CreateDbParameter("pId", _idColumn!.DbType,
            _idColumn.PropertyInfo.GetValue(objectToUpdate)!);
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
            var versionValue = _versionColumn.PropertyInfo.GetValue(objectToUpdate);
            if (versionValue == null)
            {
                sc.Query.Append(" AND ").Append(context.WrapObjectName(_versionColumn.Name)).Append(" IS NULL");
            }
            else
            {
                var pVersion = context.CreateDbParameter("pVersion", _versionColumn.DbType, versionValue);
                sc.Query.Append(" AND ").Append(context.WrapObjectName(_versionColumn.Name))
                    .Append($" = {MakeParameterName(pVersion)}");
                parameters.Add(pVersion);
            }
        }

        sc.AppendParameters(parameters);
        return sc;
    }

    public ISqlContainer BuildDelete(TID id, IDatabaseContext? context = null)
    {
        context ??= _context;
        var sc = new SqlContainer(context);

        var idCol = _idColumn;
        if (idCol == null) throw new InvalidOperationException($"row identity column for table {_tableName} not found");

        var p = _context.CreateDbParameter("id", idCol.DbType, id);
        sc.AppendParameters(p);

        sc.Query.Append("DELETE FROM ")
            .Append(_tableName)
            .Append(" WHERE ")
            .Append(context.WrapObjectName(idCol.Name));
        if (p.Value == null || p.Value == DBNull.Value)
        {
            sc.Query.Append(" IS NULL ");
        }
        else
        {
            sc.Query.Append(" = ");
            sc.Query.Append(MakeParameterName(p));
        }

        return sc;
    }

    
    private ISqlContainer BuildWhere(string wrappedColumnName, IEnumerable<TID> ids, ISqlContainer sqlContainer,
        IDatabaseContext context)
    {
        if (ids == null || ids.Count() == 0)
        {
            return sqlContainer;
        }

        ids = ids.Distinct();
        var hasNull = ids.Any(v => v == null || DBNull.Value.Equals(v));
        var sb = new StringBuilder();
        var dbType = _idColumn.DbType;
        var idx = 0;
        foreach (var id in ids)
        {
            if (!hasNull || (id != null && !DBNull.Value.Equals(id)))
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                var p = sqlContainer.AppendParameter($"p{idx++}", dbType, id);
                var name = MakeParameterName(p);
                sb.Append(name);
            }
        }

        if (sb.Length > 0)
        {
            sb.Insert(0, wrappedColumnName + " IN (");
            sb.Append(")  ");
        }

        if (hasNull)
        {
            if (sb.Length > 0) sb.Append(" AND ");
            sb.Append(wrappedColumnName + " IS NULL");
        }

        sb.Insert(0, "\n WHERE ");
        sqlContainer.Query.Append(sb);
        return sqlContainer;
    }
}