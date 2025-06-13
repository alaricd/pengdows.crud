#region

using pengdows.crud.dynamic;

#endregion

namespace WebApplication1.Generator;

public class TableDef
{
    public string Name { get; set; }
    public List<ColumnDef> Columns { get; set; } = new();
    public string? Schema { get; set; }
}