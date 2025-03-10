using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace pengdows.crud;

public class SqlContainer : ISqlContainer
{
    private readonly IDbContext _context;
    private readonly List<DbParameter> _parameters = new();

    private static readonly ConcurrentDictionary<Type, Dictionary<int, Action<object, object>>> _setterCache = new();

    public SqlContainer(IDbContext context, string query = "")
    {
        _context = context;
        Query = new StringBuilder(query);
    }

    public StringBuilder Query { get; } = new();

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

    private CommandBehavior DetermineCommandBehavior()
    {
        var behavior = CommandBehavior.SequentialAccess | CommandBehavior.SingleResult;
        if (!(_context is TransactionContext))
            behavior |= CommandBehavior.CloseConnection;
        return behavior;
    }

    private void Cleanup(DbCommand cmd, DbConnection conn)
    {
        cmd.Parameters.Clear();
        if (!(_context is TransactionContext))
            conn.Dispose();
    }

    private Dictionary<int, Action<object, object>> BuildPropertySetterMap<T>(DbDataReader reader, TableInfo tableInfo)
    {
        var setters = new Dictionary<int, Action<object, object>>();

        foreach (var column in tableInfo.Columns.Values)
        {
            var ordinal = reader.GetOrdinal(column.Name);
            if (ordinal >= 0)
            {
                var setter = CreateSetter(column.PropertyInfo);
                if (setter != null)
                    setters[ordinal] = setter;
            }
        }

        return setters;
    }

    private Action<object, object>? CreateSetter(PropertyInfo propInfo)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var value = Expression.Parameter(typeof(object), "value");

        var body = Expression.Assign(
            Expression.Property(Expression.Convert(instance, propInfo.DeclaringType!),
                                propInfo),
            Expression.Convert(value, propInfo.PropertyType));

        return Expression.Lambda<Action<object, object>>(body, instance, value).Compile();
    }

    private T MapReaderToObject<T>(DbDataReader reader) where T : new()
    {
        var obj = new T();
        var type = typeof(T);

        var setters = _setterCache.GetOrAdd(type, _ =>
        {
            var tableInfo = TypeMapRegistry.GetTableInfo<T>();
            return BuildPropertySetterMap(reader, tableInfo);
        });

        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (reader.IsDBNull(i)) continue;
            if (setters.TryGetValue(i, out var setter))
            {
                var value = reader.GetValue(i);
                setter(obj, value);
            }
        }

        return obj;
    }

    private async Task<T> ExecuteAsync<T>(Func<DbCommand, Task<T>> action, ExecutionType executionType)
    {
        var conn = _context.GetConnection(executionType);
        DbCommand? cmd = null;
        try
        {
            cmd = PrepareCommand(conn);
            return await action(cmd).ConfigureAwait(false);
        }
        finally
        {
            if (cmd != null)
                Cleanup(cmd, conn);
        }
    }

    public async Task<DbDataReader> ExecuteReaderAsync() =>
        await ExecuteAsync(async cmd =>
        {
            var behavior = DetermineCommandBehavior();
            return await cmd.ExecuteReaderAsync(behavior).ConfigureAwait(false);
        }, ExecutionType.Read);

    public async Task<T?> ExecuteScalarAsync<T>() =>
        await ExecuteAsync(async cmd =>
        {
            var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            return result is T value ? value : default;
        }, ExecutionType.Read);

    public async Task<int> ExecuteNonQueryAsync() =>
        await ExecuteAsync(cmd => cmd.ExecuteNonQueryAsync(), ExecutionType.Write);

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
            list.Add(MapReaderToObject<T>(reader));

        return list;
    }
}
