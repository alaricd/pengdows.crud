using System.Collections.Concurrent;
using System.Reflection;

namespace pengdows.crud;

public  class TypeMapRegistry:ITypeMapRegistry 
{
    private  readonly ConcurrentDictionary<Type, TableInfo> TypeMap = new();

    public  TableInfo GetTableInfo<T>()
    {
        var type = typeof(T);

        if (!TypeMap.TryGetValue(type, out var tableInfo))
        {
            tableInfo = new TableInfo
            {
                Name = type.GetCustomAttribute<TableAttribute>()?.Name ?? throw new InvalidOperationException($"Type {type.Name} does not have a TableAttribute."),
                Schema = type.GetCustomAttribute<TableAttribute>()?.Schema ?? "dbo"
            };

            foreach (var prop in type.GetProperties())
            {
                var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                if (colAttr != null)
                {
                    tableInfo.Columns[prop.Name] = new ColumnInfo
                    {
                        Name = colAttr.Name,
                        PropertyInfo = prop,
                        IsId = prop.GetCustomAttribute<IdAttribute>() != null
                    };
                }
            }

            TypeMap[type] = tableInfo;
        }

        return tableInfo;
    }
}
