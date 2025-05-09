using System.Collections.Generic;
using pengdows.crud.FakeDb;
using System;
using System.Threading.Tasks;
using Xunit;

namespace pengdows.crud.Tests;

public class DataReaderMapperTests
{
    private class SampleEntity
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public bool IsActive { get; set; }
    }

    [Fact]
    public async Task LoadObjectsFromDataReaderAsync_MapsMatchingFields()
    {
        // var reader = new FakeDbDataReader(new[]
        // {
        //     ("Name", typeof(string), "John"),
        //     ("Age", typeof(int), 30),
        //     ("IsActive", typeof(bool), true)
        // });
        var reader = new FakeDbDataReader(new[]
        {
            new Dictionary<string, object>
            {
                ["Name"] = "John",
                ["Age"] = 30,
                ["IsActive"] = true
            }
        });

        var result = await DataReaderMapper.LoadObjectsFromDataReaderAsync<SampleEntity>(reader);

        Assert.Single(result);
        Assert.Equal("John", result[0].Name);
        Assert.Equal(30, result[0].Age);
        Assert.True(result[0].IsActive);
    }

    [Fact]
    public async Task LoadObjectsFromDataReaderAsync_IgnoresUnmappedFields()
    {
        // var reader = new FakeDbDataReader(new[]
        // {
        //     ("Unrelated", typeof(string), "Ignore"),
        //     ("Name", typeof(string), "Jane"),
        // });
        var reader = new FakeDbDataReader(new[]
        {
            new Dictionary<string, object>
            {
                ["Unrelated"] = "Ignore",
                ["Name"] = "Jane"
            }
        });

        var result = await DataReaderMapper.LoadObjectsFromDataReaderAsync<SampleEntity>(reader);

        Assert.Single(result);
        Assert.Equal("Jane", result[0].Name);
    }

    [Fact]
    public async Task LoadObjectsFromDataReaderAsync_HandlesDbNullsGracefully()
    {
        // var reader = new FakeDbDataReader(new[]
        // {
        //     ("Name", typeof(string), DBNull.Value),
        //     ("Age", typeof(int), DBNull.Value),
        //     ("IsActive", typeof(bool), DBNull.Value)
        // });
        var reader = new FakeDbDataReader(new[]
        {
            new Dictionary<string, object>
            {
                ["Name"] = DBNull.Value,
                ["Age"] = DBNull.Value,
                ["IsActive"] = DBNull.Value
            }
        });

        var result = await DataReaderMapper.LoadObjectsFromDataReaderAsync<SampleEntity>(reader);

        Assert.Single(result);
        Assert.Null(result[0].Name);
        Assert.Equal(0, result[0].Age); // default(int)
        Assert.False(result[0].IsActive); // default(bool)
    }
}