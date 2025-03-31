namespace pengdows.crud;

public class TableInfo
{
    public string Schema { get; set; }
    public string Name { get; set; }
    public Dictionary<string, ColumnInfo> Columns { get; } = new();
    public ColumnInfo Id { get; set; }
    public ColumnInfo Version { get; set; }
    public ColumnInfo LastUpdatedBy { get; set; }
    public ColumnInfo LastUpdatedOn { get; set; }
    public ColumnInfo CreatedOn { get; set; }
    public ColumnInfo CreatedBy { get; set; }
}