using System.Data;
using pengdows.crud.attributes;

namespace pengdows.crud.Tests;

[Table("Sample")]
public class TestEntity
{
    [Id]
    [Column("Id", DbType.Int32)]
    public int Id { get; set; }
    
    [Column("MaxValue", DbType.Int32)]
    public int MaxValue { get; set; }
}