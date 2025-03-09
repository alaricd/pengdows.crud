using System.Collections.Concurrent;
using System.Reflection;

namespace pengdows.crud;

public class TypeMapRegistry
{
    private static readonly ConcurrentDictionary<Type, TableInfo> TypeMap = new();

    public static TableInfo GetTableInfo<T>()
    {
        var type = typeof(T);
        if (!TypeMap.TryGetValue(type, out var tableInfo))
        {
            var tableAttr = type.GetCustomAttribute<TableAttribute>()
                            ?? throw new InvalidOperationException($"Type {type.Name} does not have a TableAttribute.");

            tableInfo = new TableInfo
            {
                Name = tableAttr.Name,
                Schema = tableAttr.Schema ?? "dbo"
            };

            foreach (var prop in type.GetProperties().Where(p => p.GetCustomAttribute<ColumnAttribute>() != null))
            {
                var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                tableInfo.Columns[colAttr!.Name] = new ColumnInfo { Name = colAttr.Name, PropertyInfo = prop };
            }

            TypeMap[type] = tableInfo;
        }

        return tableInfo;
    }
}