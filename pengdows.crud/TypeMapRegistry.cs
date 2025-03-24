using System.Collections.Concurrent;
using System.Reflection;
using pengdows.crud.attributes;

namespace pengdows.crud;

public class TypeMapRegistry : ITypeMapRegistry
{
    private readonly ConcurrentDictionary<Type, TableInfo> TypeMap = new();

    public TableInfo GetTableInfo<T>()
    {
        var type = typeof(T);

        if (!TypeMap.TryGetValue(type, out var tableInfo))
        {
            tableInfo = new TableInfo
            {
                Name = type.GetCustomAttribute<TableAttribute>()?.Name ??
                       throw new InvalidOperationException($"Type {type.Name} does not have a TableAttribute."),
                Schema = type.GetCustomAttribute<TableAttribute>()?.Schema ?? ""
            };

            foreach (var prop in type.GetProperties())
            {
                var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                if (colAttr != null)
                    tableInfo.Columns[colAttr.Name] = new ColumnInfo
                    {
                        Name = colAttr.Name,
                        PropertyInfo = prop,
                        DbType = colAttr.Type,
                        IsNonUpdateable = prop.GetCustomAttribute<NonUpdateableAttribute>() != null,
                        IsId = prop.GetCustomAttribute<IdAttribute>() != null,
                        IsEnum = prop.GetCustomAttribute<EnumColumnAttribute>() != null,
                        EnumType = prop.GetCustomAttribute<EnumColumnAttribute>()?.EnumType
                    };
            }

            TypeMap[type] = tableInfo;
        }

        return tableInfo;
    }
}