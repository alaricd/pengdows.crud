using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace pengdows.crud;

public class SqlContainer : ISqlContainer
{
    private readonly IDatabaseContext _context;
    private readonly ITypeMapRegistry _typeMapRegistry;
    private readonly List<DbParameter> _parameters = new();

    // Cache for compiled property setters
    private static readonly ConcurrentDictionary<PropertyInfo, Action<object, object?>> _propertySetters = new();

    public SqlContainer(IDatabaseContext context, ITypeMapRegistry typeMapRegistry, string? query = "")
    {
        _context = context;
        _typeMapRegistry = typeMapRegistry;
        Query = new StringBuilder(query);
    }

    public StringBuilder Query { get; }

    public DbParameter AppendParameter<T>(string? name, DbType type, T value)
    {
        name ??= GenerateParameterName();
        var parameter = _context.CreateDbParameter(name, type, value);
        _parameters.Add(parameter);
        return parameter;
    }

    private string GenerateParameterName()
    {
        var dsInfo = _context.DataSourceInfo;
        return $"param{Guid.NewGuid():N}".Substring(0, Math.Min(dsInfo.ParameterNameMaxLength, 8));
    }

    private DbCommand PrepareCommand(DbConnection conn)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = Query.ToString();
        if (_parameters.Count > 0)
            cmd.Parameters.AddRange(_parameters.ToArray());
        return cmd;
    }

    private void Cleanup(DbCommand cmd, DbConnection conn)
    {
        cmd.Parameters.Clear();
        if (!(_context is TransactionContext))
            conn.Dispose();
    }

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
        var tableInfo = _typeMapRegistry.GetTableInfo<T>();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            var colName = reader.GetName(i);
            if (tableInfo.Columns.TryGetValue(colName, out var column))
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

    public async Task<int> ExecuteNonQueryAsync()
        => await ExecuteAsync(cmd => cmd.ExecuteNonQueryAsync(), ExecutionType.Write);

    public async Task<T?> ExecuteScalarAsync<T>()
        => await ExecuteAsync(async cmd => (T?)await cmd.ExecuteScalarAsync(), ExecutionType.Read);

    private async Task<TResult> ExecuteAsync<TResult>(Func<DbCommand, Task<TResult>> action, ExecutionType executionType)
    {
        var conn = _context.GetConnection(executionType);
        var cmd = PrepareCommand(conn);
        try
        {
            return await action(cmd).ConfigureAwait(false);
        }
        finally
        {
            Cleanup(cmd, conn);
        }
    }

    public async Task<DbDataReader> ExecuteReaderAsync()
        => await ExecuteAsync(cmd => cmd.ExecuteReaderAsync(), ExecutionType.Read);

    public async Task<T?> LoadSingleAsync<T>() where T : new()
    {
        await using var reader = await ExecuteReaderAsync().ConfigureAwait(false);
        return await reader.ReadAsync().ConfigureAwait(false) ? MapReaderToObject<T>(reader) : default;
    }

    public async Task<List<T>> LoadListAsync<T>() where T : new()
    {
        var list = new List<T>();
        await using var reader = await ExecuteReaderAsync().ConfigureAwait(false);

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(MapReaderToObject<T>(reader));
        }

        return list;
    }
}