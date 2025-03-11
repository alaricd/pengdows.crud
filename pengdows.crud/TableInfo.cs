namespace pengdows.crud;

public class TableInfo
{
    public string Schema { get; set; }
    public string Name { get; set; }
    public Dictionary<string, ColumnInfo> Columns { get; } = new();
}