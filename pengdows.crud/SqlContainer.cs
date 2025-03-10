using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;

namespace pengdows.crud;

public class SqlContainer:ISqlContainer
{
    private readonly IDbContext _context;
    private readonly List<DbParameter> _parameters = new();

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
        ClearCommand(cmd);
        if (!(_context is TransactionContext))
            conn.Dispose();
    }

    private void ClearCommand(DbCommand cmd)
    {
        cmd?.Parameters?.Clear();
    }

    public async Task<DbDataReader> ExecuteReaderAsync()
    {
        var conn = _context.GetConnection(ExecutionType.Read);
        DbCommand cmd = null;
        try
        {
            cmd = PrepareCommand(conn);
            var behavior = DetermineCommandBehavior();
            var reader = await cmd.ExecuteReaderAsync(behavior);
            ClearCommand(cmd); // Ensure parameters are cleared after execution
            return reader;
        }
        catch
        {
            Cleanup(cmd, conn);
            throw;
        }
    }

    public async Task<T?> ExecuteScalarAsync<T>()
    {
        var conn = _context.GetConnection(ExecutionType.Read);
        DbCommand cmd = null;
        try
        {
            cmd = PrepareCommand(conn);
            var result = await cmd.ExecuteScalarAsync();
            return result is T value ? value : default;
        }
        finally
        {
            Cleanup(cmd, conn);
        }
    }

    public async Task<int> ExecuteNonQueryAsync()
    {
        var conn = _context.GetConnection(ExecutionType.Write);
        DbCommand? cmd = null;
        try
        {
            cmd = PrepareCommand(conn);
            var result = await cmd.ExecuteNonQueryAsync();
            return result;
        }
        finally
        {
            Cleanup(cmd, conn);
        }
    }

    private T MapReaderToObject<T>(DbDataReader reader, Dictionary<int, PropertyInfo>? map = null) where T : new()
    {
        var obj = new T();
        var tableInfo = TypeMapRegistry.GetTableInfo<T>();
        map ??= CreateMap(reader, tableInfo);
        
        foreach (var column in tableInfo.Columns.Values)
        {
            var value = reader[column.Name];
            if (value != DBNull.Value)
            {
                column.PropertyInfo.SetValue(obj, value);
            }
        }

        return obj;
    }

    private Dictionary<int, PropertyInfo> CreateMap(DbDataReader reader, TableInfo tableInfo)
    {
       var map = new  Dictionary<int, PropertyInfo>();
       foreach(var itm in tableInfo.Columns)
       {
          var idx =  reader.GetOrdinal(itm.Value.Name);
          if (idx >-1)
          {
              map.Add(idx, itm.Value.PropertyInfo);
          }
       }

       return map;
    }

    public async Task<T?> LoadSingleAsync<T>() where T : new()
    {
        await using var reader = await ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapReaderToObject<T>(reader) : default;
    }

    public async Task<List<T>> LoadListAsync<T>() where T : new()
    {
        var list = new List<T>();
        await using var reader = await ExecuteReaderAsync();
var map = CreateMap(reader, TypeMapRegistry.GetTableInfo<T>());
        while (await reader.ReadAsync())
        {
            var item = MapReaderToObject<T>(reader, map);
            list.Add(item);
        }

        return list;
    }
}
