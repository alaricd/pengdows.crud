using System.Data;
using pengdows.crud.attributes;
using Xunit;

namespace pengdows.crud.Tests;

public class TypeMapRegistryTests
{
    [Fact]
    public void Register_AddsAndRetrievesTableInfo()
    {
        var registry = new TypeMapRegistry();
        registry.Register<MyEntity>();

        var info = registry.GetTableInfo<MyEntity>();
        Assert.NotNull(info);
        Assert.Equal("Id", info.Id?.Name);
    }

    [Table("MyEntity")]
    private class MyEntity
    {
        [Id]
        [Column("Id", DbType.Int32)]
        public int Id { get; set; }
    }
}