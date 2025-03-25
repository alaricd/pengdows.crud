using System.Data;
using pengdows.crud.attributes;

namespace testbest;

[Table("test_table")]
public class TestTable
{
    [Id] [Column("id", DbType.Int64)] public long Id { get; set; }

    [Column("name", DbType.String)]
    [EnumColumn(typeof(NameEnum))]
    public NameEnum? Name { get; set; }

    [Column("created_at", DbType.DateTime)]
    public DateTime CreatedAt { get; set; }
    
}

public enum NameEnum
{
    Test
}