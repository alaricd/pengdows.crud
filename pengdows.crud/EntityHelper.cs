using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using pengdows.crud.enums;

namespace pengdows.crud;

public class EntityHelper<T, TID> : IEntityHelper<T, TID> where T : class, new()
{
    // Cache for compiled property setters
    private static readonly ConcurrentDictionary<PropertyInfo, Action<object, object?>> _propertySetters = new();
    private readonly IDatabaseContext _context;
    private readonly ColumnInfo? _idColumn;
    private readonly string _parameterMarker;
    private readonly TableInfo _tableInfo;
    private readonly ITypeMapRegistry _typeMap;
    private readonly bool _usePositionalParameters;

    private readonly ColumnInfo? _versionColumn;

    public EntityHelper(IDatabaseContext databaseContext,
        EnumParseFailureMode enumParseBehavior = EnumParseFailureMode.Throw) :
        this(databaseContext, databaseContext.TypeMapRegistry)
    {
    }

    public EntityHelper(IDatabaseContext databaseContext, ITypeMapRegistry typeMap,
        EnumParseFailureMode enumParseBehavior = EnumParseFailureMode.Throw)
    {
        _context = databaseContext;
        _typeMap = typeMap;
        _tableInfo = _typeMap.GetTableInfo<T>() ??
                     throw new InvalidOperationException($"Type {typeof(T).FullName} is not a table.");
        _parameterMarker = _context.DataSourceInfo.ParameterMarker;
        _usePositionalParameters = _context.DataSourceInfo.ParameterMarker == "?";

        WrappedWrappedTableName = (!string.IsNullOrEmpty(_tableInfo.Schema)
                                      ? _context.WrapObjectName(_tableInfo.Schema) +
                                        _context.DataSourceInfo.CompositeIdentifierSeparator
                                      : "")
                                  + _context.WrapObjectName(_tableInfo.Name);
        _idColumn = _tableInfo.Columns.Values.FirstOrDefault(itm => itm.IsId);
        _versionColumn = _tableInfo.Columns.Values.FirstOrDefault(itm => itm.IsVersion);
        EnumParseBehavior = enumParseBehavior;
    }

    public string WrappedWrappedTableName { get; }

    public EnumParseFailureMode EnumParseBehavior { get; set; }


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
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);

                var dbFieldType = reader.GetFieldType(i);
                value = TypeCoercionHelper.Coerce(value, dbFieldType, column);

                var setter = GetOrCreateSetter(column.PropertyInfo);
                setter(obj, value);
            }
        }

        return obj;
    }
    //
    // private object? CoerceData(object? value, Type dbFieldType, Type propertyType, ColumnInfo column)
    // {
    //     if (value == null) return null;
    //
    //     // No coercion needed if types match
    //     if (dbFieldType == propertyType)
    //         return value;
    //
    //     // Enum coercion
    //     if (column.EnumType != null)
    //     {
    //         if (Enum.TryParse(column.EnumType, value.ToString(), true, out var result))
    //             return result;
    //
    //         switch (EnumParseBehavior)
    //         {
    //             case EnumParseFailureMode.Throw:
    //                 throw new ArgumentException($"Cannot convert value '{value}' to enum {column.EnumType}.");
    //
    //             case EnumParseFailureMode.SetNullAndLog:
    //                 if (Nullable.GetUnderlyingType(propertyType) == column.EnumType)
    //                     return null;
    //                 throw new ArgumentException($"Cannot convert '{value}' to non-nullable enum {column.EnumType}.");
    //
    //             case EnumParseFailureMode.SetDefaultValue:
    //                 return Enum.GetValues(column.EnumType).GetValue(0);
    //         }
    //     }
    //
    //     if (column.IsJsonType)
    //     {
    //         if (value is string json && !string.IsNullOrWhiteSpace(json))
    //         {
    //             return JsonSerializer.Deserialize(json, propertyType, column.JsonSerializerOptions); 
    //         }
    //
    //         throw new ArgumentException($"Cannot deserialize JSON value '{value}' to type {propertyType}.");
    //     }
    //
    //     // DateTime coercion
    //     if (dbFieldType == typeof(string) && propertyType == typeof(DateTime) && value is string s)
    //         return DateTime.Parse(s);
    //
    //     // Guid coercion
    //     if (propertyType == typeof(Guid))
    //     {
    //         if (value is string guidStr && Guid.TryParse(guidStr, out var guid))
    //             return guid;
    //         if (value is byte[] bytes && bytes.Length == 16)
    //             return new Guid(bytes);
    //     }
    //
    //     // Future: handle decimal to int64 for Oracle or other quirks
    //     try
    //     {
    //         var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
    //         return Convert.ChangeType(value, targetType);
    //     }
    //     catch (Exception ex)
    //     {
    //         if (EnumParseBehavior == EnumParseFailureMode.Throw)
    //             throw new InvalidCastException($"Failed to convert value '{value}' to {propertyType}", ex);
    //     }
    //
    //     // Fallback — no conversion
    //     return value;
    // }

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
        if (await reader.ReadAsync().ConfigureAwait(false)) return MapReaderToObject(reader);

        return null;
    }

    public async Task<List<T>> LoadListAsync(ISqlContainer sc)
    {
        var list = new List<T>();
        var reader = await sc.ExecuteReaderAsync().ConfigureAwait(false);

        while (await reader.ReadAsync().ConfigureAwait(false)) list.Add(MapReaderToObject(reader));

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
            var value = column.MakeParameterValueFromField(objectToCreate);
            var p = _context.CreateDbParameter(paramName,
                column.DbType,
                value
            );

            columns.Append(_context.WrapObjectName(column.Name));
            if (Utils.IsNullOrDbNull(value))
            {
                values.Append("NULL");
            }
            else
            {
                values.Append(MakeParameterName(p));
                parameters.Add(p);
            }
        }

        sc.Query.Append("INSERT INTO ")
            .Append(WrappedWrappedTableName)
            .Append(" (")
            .Append(columns)
            .Append(") VALUES (")
            .Append(values)
            .Append(")");

        sc.AppendParameters(parameters);
        return sc;
    }

    public ISqlContainer BuildBaseRetrieve(string alias, IDatabaseContext? context = null)
    {
        context ??= _context;
        var wrappedAlias = "";
        if (!string.IsNullOrWhiteSpace(alias))
            wrappedAlias = context.WrapObjectName(alias) +
                           context.DataSourceInfo.CompositeIdentifierSeparator;

        var sc = new SqlContainer(context);
        var sb = sc.Query;
        sb.Append("SELECT ");
        sb.Append(string.Join(", ", _tableInfo.Columns.Values.Select(col => string.Format("{0}{1}",
            wrappedAlias,
            context.WrapObjectName(col.Name)))));
        sb.Append(" FROM ").Append(WrappedWrappedTableName);
        sb.Append(" " + wrappedAlias.Substring(0, wrappedAlias.Length - 1));

        return sc;
    }

    public ISqlContainer BuildRetrieve(List<TID>? listOfIds = null, IDatabaseContext? context = null,
        string alias = "a")
    {
        context ??= _context;
        var sc = BuildBaseRetrieve(alias, context);
        var wrappedAlias = "";
        if (!string.IsNullOrWhiteSpace(alias))
            wrappedAlias = context.WrapObjectName(alias) +
                           context.DataSourceInfo.CompositeIdentifierSeparator;

        var wrappedColumnName = wrappedAlias +
                                _context.WrapObjectName(_idColumn.Name);
        BuildWhere(
            wrappedColumnName,
            listOfIds,
            sc,
            context);

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
                //Skip columns that should never be directly updated
                //the version column, rowId and columns we have marked 
                //as non-updateable
                continue;

            var newValue = column.MakeParameterValueFromField(objectToUpdate);
            var originalValue = loadOriginal ? column.MakeParameterValueFromField(original) : null;

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
            //this should be updated to wrap other patterns.
            setClause.Append(
                $", {_context.WrapObjectName(_versionColumn.Name)} = {_context.WrapObjectName(_versionColumn.Name)} + 1");

        if (setClause.Length == 0)
            throw new InvalidOperationException("No changes detected for update.");

        var pId = context.CreateDbParameter("pId", _idColumn!.DbType,
            _idColumn.PropertyInfo.GetValue(objectToUpdate)!);
        parameters.Add(pId);

        sc.Query.Append("UPDATE ")
            .Append(WrappedWrappedTableName)
            .Append(" SET ")
            .Append(setClause)
            .Append(" WHERE ")
            .Append(context.WrapObjectName(_idColumn.Name))
            .Append($" = {MakeParameterName(pId)}");

        if (_versionColumn != null)
        {
            var versionValue = _versionColumn.MakeParameterValueFromField(objectToUpdate);
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
        if (idCol == null)
            throw new InvalidOperationException($"row identity column for table {WrappedWrappedTableName} not found");

        var p = _context.CreateDbParameter("id", idCol.DbType, id);
        sc.AppendParameters(p);

        sc.Query.Append("DELETE FROM ")
            .Append(WrappedWrappedTableName)
            .Append(" WHERE ")
            .Append(context.WrapObjectName(idCol.Name));
        if (Utils.IsNullOrDbNull(p.Value))
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
        var enumerable = ids?.Distinct().ToList();
        if (enumerable == null || enumerable.Count == 0) return sqlContainer;


        var hasNull = enumerable.Any(v => Utils.IsNullOrDbNull(v));
        var sb = new StringBuilder();
        var dbType = _idColumn!.DbType;
        var idx = 0;
        foreach (var id in enumerable)
            if (!hasNull || !Utils.IsNullOrDbNull(id))
            {
                if (sb.Length > 0) sb.Append(", ");

                var p = sqlContainer.AppendParameter($"p{idx++}", dbType, id);
                var name = MakeParameterName(p);
                sb.Append(name);
            }

        if (sb.Length > 0)
        {
            sb.Insert(0, wrappedColumnName + " IN (");
            sb.Append(")  ");
        }

        if (hasNull)
        {
            if (sb.Length > 0) sb.Append("\nOR ");
            sb.Append(wrappedColumnName + " IS NULL");
        }

        sb.Insert(0, "\nWHERE ");
        sqlContainer.Query.Append(sb);
        return sqlContainer;
    }
}