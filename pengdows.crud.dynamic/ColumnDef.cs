using System.Data;

namespace pengdows.crud.dynamic;

public class ColumnDef
{
    public string Name { get; set; }
    public string Type { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public DbType DbType { get; set; }
    public bool IsUpdatedBy { get; set; }
    public bool IsUpdatedOn { get; set; }
    public bool IsCreatedOn { get; set; }
    public bool IsCreatedBy { get; set; }
    public bool IsIdCandidate { get; set; }
    public bool Insertable { get; set; }
    public int? PrimaryKeyOrder { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsAutoIncrement { get; set; }
    public int Ordinal { get; set; }
}