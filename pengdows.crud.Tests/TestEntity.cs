using System.Data;
using pengdows.crud.attributes;

namespace pengdows.crud.Tests;

[Table("Test")]
public class TestEntity
{
    [Id] [Column("Id", DbType.Int32)] public int Id { get; set; }

    [Column("Name", DbType.String)] public string Name { get; set; }

    [Version]
    [Column("Version", DbType.Int32)]
    public int version { get; set; }
}