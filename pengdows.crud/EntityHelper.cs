#region

using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using pengdows.crud.attributes;
using pengdows.crud.enums;

#endregion

namespace pengdows.crud;

public class EntityHelper<T, TID> : IEntityHelper<T, TID> where T : class, new()
{
    // Cache for compiled property setters
    private static readonly ConcurrentDictionary<PropertyInfo, Action<object, object?>> _propertySetters = new();
    private readonly IDatabaseContext _context;
    private readonly ColumnInfo? _idColumn;
    private readonly string _parameterMarker;
    private readonly IServiceProvider _serviceProvider;
    private readonly TableInfo _tableInfo;
    private readonly bool _usePositionalParameters;

    private readonly ColumnInfo? _versionColumn;
    private readonly Type? _userFieldType = null;

    public EntityHelper(IDatabaseContext databaseContext,
        IServiceProvider serviceProvider,
        EnumParseFailureMode enumParseBehavior = EnumParseFailureMode.Throw
    )
    {
        _context = databaseContext;
        _serviceProvider = serviceProvider;
        var typemap = serviceProvider?.GetService<ITypeMapRegistry>() ?? new TypeMapRegistry();

        _tableInfo = typemap.GetTableInfo<T>() ??
                     throw new InvalidOperationException($"Type {typeof(T).FullName} is not a table.");
        var propertyInfoPropertyType = _tableInfo.Columns
            .Values
            .FirstOrDefault(c =>
                c.PropertyInfo.GetCustomAttribute<CreatedByAttribute>() != null ||
                c.PropertyInfo.GetCustomAttribute<LastUpdatedByAttribute>() != null
            )?.PropertyInfo.PropertyType;
        if (propertyInfoPropertyType != null && _serviceProvider != null)
        {
            _userFieldType = propertyInfoPropertyType;
        }

        _parameterMarker = _context.DataSourceInfo.ParameterMarker;
        _usePositionalParameters = _context.DataSourceInfo.ParameterMarker == "?";

        WrappedTableName = (!string.IsNullOrEmpty(_tableInfo.Schema)
                               ? WrapObjectName(_tableInfo.Schema) +
                                 _context.DataSourceInfo.CompositeIdentifierSeparator
                               : "")
                           + WrapObjectName(_tableInfo.Name);
        _idColumn = _tableInfo.Columns.Values.FirstOrDefault(itm => itm.IsId);
        _versionColumn = _tableInfo.Columns.Values.FirstOrDefault(itm => itm.IsVersion);
        EnumParseBehavior = enumParseBehavior;
    }

    public string WrappedTableName { get; }

    public EnumParseFailureMode EnumParseBehavior { get; set; }


    public string MakeParameterName(DbParameter p)
    {
        return _context.MakeParameterName(p);
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


    public Task<T?> RetrieveOneAsync(T objectToRetrieve, IDatabaseContext? context = null)
    {
        var ctx = context ?? _context;
        var id = (TID)_idColumn.PropertyInfo.GetValue(objectToRetrieve);
        var list = new List<TID>() { id };
        var sc = BuildRetrieve(list, null, ctx);
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

        await using var reader = await sc.ExecuteReaderAsync().ConfigureAwait(false);

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var obj = MapReaderToObject(reader);
            if (obj != null)
            {
                list.Add(obj);
            }
        }

        return list;
    }


    public ISqlContainer BuildCreate(T objectToCreate, IDatabaseContext? context = null)
    {
        if (objectToCreate == null)
            throw new ArgumentNullException(nameof(objectToCreate));
        var ctx = context ?? _context;
        var columns = new StringBuilder();
        var values = new StringBuilder();
        var parameters = new List<DbParameter>();
        var pid = 0;
        var sc = ctx.CreateSqlContainer();
        SetAuditFields(objectToCreate, false);
        foreach (var column in _tableInfo.Columns.Values)
        {
            if (column.IsId && !column.IsIdIsWritable) continue;

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

            columns.Append(WrapObjectName(column.Name));
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
            .Append(WrappedTableName)
            .Append(" (")
            .Append(columns)
            .Append(") VALUES (")
            .Append(values)
            .Append(")");

        sc.AddParameters(parameters);
        return sc;
    }


    public ISqlContainer BuildBaseRetrieve(string alias, IDatabaseContext? context = null)
    {
        var wrappedAlias = "";
        if (!string.IsNullOrWhiteSpace(alias))
            wrappedAlias = WrapObjectName(alias) +
                           _context.DataSourceInfo.CompositeIdentifierSeparator;
        var ctx = context ?? _context;
        var sc = ctx.CreateSqlContainer();
        var sb = sc.Query;
        sb.Append("SELECT ");
        sb.Append(string.Join(", ", _tableInfo.Columns.Values.Select(col => string.Format("{0}{1}",
            wrappedAlias,
            WrapObjectName(col.Name)))));
        sb.Append("\nFROM ").Append(WrappedTableName);
        if (wrappedAlias.Length > 0)
        {
            sb.Append(" " + wrappedAlias.Substring(0, wrappedAlias.Length - 1));
        }

        return sc;
    }

    public ISqlContainer BuildRetrieve(List<TID>? listOfIds = null,
        string alias = "a", IDatabaseContext? context = null)
    {
        var ctx = context ?? _context;
        var sc = BuildBaseRetrieve(alias, ctx);
        var wrappedAlias = "";
        if (!string.IsNullOrWhiteSpace(alias))
            wrappedAlias = WrapObjectName(alias) +
                           _context.DataSourceInfo.CompositeIdentifierSeparator;

        var wrappedColumnName = wrappedAlias +
                                WrapObjectName(_idColumn.Name);
        BuildWhere(
            wrappedColumnName,
            listOfIds,
            sc
        );

        return sc;
    }

    public ISqlContainer BuildRetrieve(List<T>? listOfObjects = null,
        string alias = "a", IDatabaseContext context = null)
    {
        var ctx = context ?? _context;
        var sc = BuildBaseRetrieve(alias, ctx);
        var wrappedAlias = "";
        if (!string.IsNullOrWhiteSpace(alias))
            wrappedAlias = WrapObjectName(alias) +
                           _context.DataSourceInfo.CompositeIdentifierSeparator;

        var wrappedColumnName = wrappedAlias +
                                WrapObjectName(_idColumn.Name);
        BuildWhereByPrimaryKey(
            listOfObjects,
            sc);

        return sc;
    }

    public ISqlContainer BuildRetrieve(List<TID>? listOfIds = null, IDatabaseContext? context = null)
    {
        return BuildRetrieve(listOfIds, null, context);
    }

    public ISqlContainer BuildRetrieve(List<T>? listOfObjects = null, IDatabaseContext? context = null)
    {
        return BuildRetrieve(listOfObjects, null, context);
    }

    public void BuildWhereByPrimaryKey(List<T>? listOfObjects, ISqlContainer sc, string alias = "")
    {
        if (Utils.IsNullOrEmpty(listOfObjects) || sc == null)
        {
            throw new ArgumentException("List of objects cannot be null or empty.");
        }

        var listOfPrimaryKeys = _tableInfo.Columns.Values.Where(o => o.IsPrimaryKey).ToList();
        if (listOfPrimaryKeys.Count < 1)
        {
            throw new Exception($"No primary keys found for type {typeof(T).Name}");
        }

        // Calculate total parameter count to avoid exceeding DB limits
        var pc = sc.ParameterCount;
        var numberOfParametersToBeAdded = listOfObjects.Count * listOfPrimaryKeys.Count;
        if ((pc + numberOfParametersToBeAdded) > _context.MaxParameterLimit)
        {
            throw new TooManyParametersException("Too many parameters", _context.MaxParameterLimit);
        }

        var sb = new StringBuilder();
        var pp = new List<DbParameter>();

        // Wrap alias if provided
        var wrappedAlias = string.IsNullOrWhiteSpace(alias)
            ? ""
            : WrapObjectName(alias) + _context.CompositeIdentifierSeparator;

        // Construct WHERE clause as series of (pk1 = val AND pk2 = val) OR (...)...
        for (var i = 0; i < listOfObjects.Count; i++)
        {
            var entity = listOfObjects[i];
            if (i > 0)
            {
                sb.Append(" OR ");
            }

            sb.Append("(");

            for (var j = 0; j < listOfPrimaryKeys.Count; j++)
            {
                if (j > 0)
                {
                    sb.Append(" AND ");
                }

                var pk = listOfPrimaryKeys[j];
                var value = pk.MakeParameterValueFromField(entity);

                // Create parameter with unique and valid name auto-generated by context
                var parameter = _context.CreateDbParameter(pk.DbType, value);

                sb.Append(wrappedAlias);
                sb.Append(WrapObjectName(pk.Name));

                if (Utils.IsNullOrDbNull(value))
                {
                    sb.Append(" IS NULL");
                }
                else
                {
                    sb.Append(" = ");
                    sb.Append(MakeParameterName(parameter));
                    pp.Add(parameter);
                }
            }

            sb.Append(")");
        }

        if (sb.Length < 1)
        {
            return;
        }

        // Add all generated parameters to the container
        sc.AddParameters(pp);

        // Determine how to append WHERE/AND clause
        var query = sc.Query.ToString();
        if (!query.Contains("WHERE ", StringComparison.OrdinalIgnoreCase))
        {
            sc.Query.Append("\n WHERE ");
        }
        else
        {
            sc.Query.Append("\n AND ");
        }

        // Final WHERE clause with grouped filters
        sc.Query.Append(sb);
    }


    public Task<ISqlContainer> BuildUpdateAsync(T objectToUpdate, IDatabaseContext? context = null)
    {
        var ctx = context ?? _context;
        return BuildUpdateAsync(objectToUpdate, _versionColumn != null, ctx);
    }

    public async Task<ISqlContainer> BuildUpdateAsync(T objectToUpdate, bool loadOriginal,
        IDatabaseContext? context = null)
    {
        if (objectToUpdate == null)
            throw new ArgumentNullException(nameof(objectToUpdate));

        context ??= _context;
        var setClause = new StringBuilder();
        var parameters = new List<DbParameter>();
        SetAuditFields(objectToUpdate, true);
        var sc = context.CreateSqlContainer();
        var original = null as T;

        if (loadOriginal)
        {
            original = await RetrieveOneAsync(objectToUpdate);
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
                setClause.Append($"{WrapObjectName(column.Name)} = NULL");
            }
            else
            {
                var paramName = $"p{parameters.Count}";
                var param = context.CreateDbParameter(paramName, column.DbType, newValue);
                parameters.Add(param);
                setClause.Append($"{WrapObjectName(column.Name)} = {MakeParameterName(param)}");
            }
        }

        if (_versionColumn != null)
            //this should be updated to wrap other patterns.
            setClause.Append(
                $", {WrapObjectName(_versionColumn.Name)} = {WrapObjectName(_versionColumn.Name)} + 1");

        if (setClause.Length == 0)
            throw new InvalidOperationException("No changes detected for update.");

        var pId = context.CreateDbParameter("pId", _idColumn!.DbType,
            _idColumn.PropertyInfo.GetValue(objectToUpdate)!);
        parameters.Add(pId);

        sc.Query.Append("UPDATE ")
            .Append(WrappedTableName)
            .Append(" SET ")
            .Append(setClause)
            .Append(" WHERE ")
            .Append(WrapObjectName(_idColumn.Name))
            .Append($" = {MakeParameterName(pId)}");

        if (_versionColumn != null)
        {
            var versionValue = _versionColumn.MakeParameterValueFromField(objectToUpdate);
            if (versionValue == null)
            {
                sc.Query.Append(" AND ").Append(WrapObjectName(_versionColumn.Name)).Append(" IS NULL");
            }
            else
            {
                var pVersion = context.CreateDbParameter("pVersion", _versionColumn.DbType, versionValue);
                sc.Query.Append(" AND ").Append(WrapObjectName(_versionColumn.Name))
                    .Append($" = {MakeParameterName(pVersion)}");
                parameters.Add(pVersion);
            }
        }

        sc.AddParameters(parameters);
        return sc;
    }

    public ISqlContainer BuildDelete(TID id, IDatabaseContext? context = null)
    {
        var ctx = context ?? _context;
        var sc = ctx.CreateSqlContainer();

        var idCol = _idColumn;
        if (idCol == null)
            throw new InvalidOperationException($"row identity column for table {WrappedTableName} not found");

        var p = _context.CreateDbParameter("id", idCol.DbType, id);
        sc.AddParameter(p);

        sc.Query.Append("DELETE FROM ")
            .Append(WrappedTableName)
            .Append(" WHERE ")
            .Append(WrapObjectName(idCol.Name));
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


    public ISqlContainer BuildWhere(string wrappedColumnName, IEnumerable<TID> ids, ISqlContainer sqlContainer)
    {
        var enumerable = ids?.Distinct().ToList();
        if (Utils.IsNullOrEmpty(enumerable)) return sqlContainer;


        var hasNull = enumerable.Any(v => Utils.IsNullOrDbNull(v));
        var sb = new StringBuilder();
        var dbType = _idColumn!.DbType;
        var idx = 0;
        foreach (var id in enumerable)
            if (!hasNull || !Utils.IsNullOrDbNull(id))
            {
                if (sb.Length > 0) sb.Append(", ");

                var p = sqlContainer.AddParameterWithValue($"p{idx++}", dbType, id);
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


    private void SetAuditFields(T obj, bool updateOnly)
    {
        if (_userFieldType == null || _serviceProvider == null || obj == null)
            return;

        var (userId, now) = AuditFieldResolver.ResolveFrom(_userFieldType, _serviceProvider);

        // Always update last-modified
        _tableInfo.LastUpdatedBy?.PropertyInfo?.SetValue(obj, userId);
        _tableInfo.LastUpdatedOn?.PropertyInfo?.SetValue(obj, now);

        if (updateOnly) return;

        // Only set Created fields if they are null or default
        if (_tableInfo.CreatedBy?.PropertyInfo != null)
        {
            var currentValue = _tableInfo.CreatedBy.PropertyInfo.GetValue(obj);
            if (currentValue == null
                || currentValue as string == string.Empty
                || Utils.IsZeroNumeric(currentValue))
                _tableInfo.CreatedBy.PropertyInfo.SetValue(obj, userId);
        }

        if (_tableInfo.CreatedOn?.PropertyInfo != null)
        {
            var currentValue = _tableInfo.CreatedOn.PropertyInfo.GetValue(obj) as DateTime?;
            if (currentValue == null || currentValue == default(DateTime))
                _tableInfo.CreatedOn.PropertyInfo.SetValue(obj, now);
        }
    }

    private string WrapObjectName(string objectName)
    {
        return _context.WrapObjectName(objectName);
    }
    //
    // public ISqlContainer BuildUpsert(T objectToUpsert, IDatabaseContext? context = null)
    // {
    //     var ctx = context ?? _context;
    //     return _context.DataSourceInfo.Product
    //         switch
    //         {
    //             SupportedDatabase.PostgreSql or SupportedDatabase.Sqlite or SupportedDatabase.CockroachDb =>
    //                 BuildInsertOnConflictUpsert(objectToUpsert, ctx),
    //
    //             SupportedDatabase.MySql or SupportedDatabase.MariaDb =>
    //                 BuildInsertOnDuplicateKeyUpsert(objectToUpsert, ctx),
    //
    //             SupportedDatabase.SqlServer or SupportedDatabase.Oracle or SupportedDatabase.Firebird =>
    //                 BuildMergeUpsert(objectToUpsert, ctx),
    //
    //             _ => throw new NotSupportedException("UPSERT is not supported for this database.")
    //         };
    // }
    //
    // private ISqlContainer BuildInsertOnConflictUpsert(T obj, IDatabaseContext ctx)
    // {
    //     var sc = BuildInsertStatement(obj, ctx, out var updateClause, out var conflictColumn);
    //
    //     sc.Query.Append(" ON CONFLICT(")
    //         .Append(WrapObjectName(conflictColumn.Name))
    //         .Append(") DO UPDATE SET ")
    //         .Append(updateClause);
    //
    //     return sc;
    // }
    //
    // private ISqlContainer BuildInsertOnDuplicateKeyUpsert(T obj, IDatabaseContext ctx)
    // {
    //     var sc = BuildInsertStatement(obj, ctx, out var updateClause, out _);
    //
    //     sc.Query.Append(" ON DUPLICATE KEY UPDATE ")
    //         .Append(updateClause);
    //
    //     return sc;
    // }
    //
    // private ISqlContainer BuildMergeUpsert(T objectToUpsert, IDatabaseContext ctx)
    // {
    //     if (objectToUpsert == null)
    //         throw new ArgumentNullException(nameof(objectToUpsert));
    //     if (_idColumn == null)
    //         throw new InvalidOperationException($"No ID column defined for type {typeof(T).Name}");
    //
    //     var sc = ctx.CreateSqlContainer();
    //     var parameters = new List<DbParameter>();
    //     var sourceAlias = WrapObjectName("source");
    //     var targetAlias = WrapObjectName("target");
    //
    //     var insertColumns = new List<string>();
    //     var sourceSelect = new List<string>();
    //     var insertValues = new List<string>();
    //     var updateAssignments = new List<string>();
    //
    //     int paramIndex = 0;
    //
    //     SetAuditFields(objectToUpsert, updateOnly: false);
    //
    //     foreach (var column in _tableInfo.Columns.Values)
    //     {
    //         if (column.IsId && !column.IsIdIsWritable) continue;
    //
    //         var value = column.MakeParameterValueFromField(objectToUpsert);
    //         var param = ctx.CreateDbParameter($"p{paramIndex++}", column.DbType, value);
    //         parameters.Add(param);
    //
    //         var wrappedCol = WrapObjectName(column.Name);
    //         var paramRef = MakeParameterName(param);
    //
    //         insertColumns.Add(wrappedCol);
    //         insertValues.Add($"{sourceAlias}.{wrappedCol}");
    //         sourceSelect.Add($"{paramRef} AS {wrappedCol}");
    //
    //         if (!column.IsId && !column.IsVersion && !column.IsNonUpdateable)
    //         {
    //             updateAssignments.Add($"{targetAlias}.{wrappedCol} = {sourceAlias}.{wrappedCol}");
    //         }
    //     }
    //
    //     if (_versionColumn != null)
    //     {
    //         var versionCol = WrapObjectName(_versionColumn.Name);
    //         updateAssignments.Add($"{targetAlias}.{versionCol} = {targetAlias}.{versionCol} + 1");
    //     }
    //
    //     var idColName = WrapObjectName(_idColumn.Name);
    //     var fromClause = _context.DataSourceInfo.Product switch
    //     {
    //         SupportedDatabase.Oracle => "FROM DUAL",
    //         SupportedDatabase.Firebird => "FROM RDB$DATABASE",
    //         SupportedDatabase.SqlServer => "FROM (SELECT 1 AS Dummy) AS dummy",
    //         _ => throw new NotSupportedException("MERGE UPSERT not supported for this database.")
    //     };
    //
    //     sc.Query.AppendLine("MERGE INTO ")
    //         .Append(WrappedTableName).Append(" ").Append(targetAlias).AppendLine()
    //         .Append("USING (SELECT ").Append(string.Join(", ", sourceSelect)).Append(" ")
    //         .Append(fromClause).Append(") ").Append(sourceAlias).AppendLine()
    //         .Append("ON (")
    //         .Append($"{targetAlias}.{idColName} = {sourceAlias}.{idColName})").AppendLine()
    //         .AppendLine("WHEN MATCHED THEN")
    //         .Append("  UPDATE SET ").Append(string.Join(", ", updateAssignments)).AppendLine()
    //         .AppendLine("WHEN NOT MATCHED THEN")
    //         .Append("  INSERT (").Append(string.Join(", ", insertColumns)).Append(")").AppendLine()
    //         .Append("  VALUES (").Append(string.Join(", ", insertValues)).Append(");");
    //
    //     sc.AddParameters(parameters);
    //     return sc;
    // }
    //
    // private string GetUpsertExcludedValue(ColumnInfo col)
    // {
    //     return _context.DataSourceInfo.Product switch
    //     {
    //         SupportedDatabase.PostgreSql or SupportedDatabase.Sqlite or SupportedDatabase.CockroachDb =>
    //             $"excluded.{WrapObjectName(col.Name)}",
    //
    //         SupportedDatabase.MySql or SupportedDatabase.MariaDb =>
    //             $"VALUES({WrapObjectName(col.Name)})",
    //
    //         _ => throw new NotSupportedException("Excluded values not supported for this database.")
    //     };
    // }
}