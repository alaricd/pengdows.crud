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
}