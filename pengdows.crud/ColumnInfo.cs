using System.Data;
using System.Reflection;

namespace pengdows.crud;

public class ColumnInfo
{
    public string Name { get; init; }
    public PropertyInfo PropertyInfo { get; init; }
    public bool IsId { get; init; } = false;
    public DbType DbType { get; set; }
    public bool IsVersion { get; set; }
    public bool IsNonUpdateable { get; set; }
    public bool IsEnum { get; set; }
    public Type? EnumType { get; set; }

    public object? MakeParameterValueFromField<T>(T objectToCreate) 
    {
        var value = this.PropertyInfo.GetValue(objectToCreate);
        if (value != null)
        { 
            if (this.EnumType != null)
            {
                value = this.DbType == DbType.String
                    ? value.ToString() // Save enum as string name
                    : Convert.ChangeType(value, Enum.GetUnderlyingType(this.EnumType)); // Save enum as int
            }
        }

        return value;
    }
}