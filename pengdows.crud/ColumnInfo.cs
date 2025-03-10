using System.Reflection;

namespace pengdows.crud;

public class ColumnInfo
{
    public string Name { get; set; }
    public PropertyInfo PropertyInfo { get; set; }
    public bool IsId { get; set; } = false;
}