using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using pengdows.crud.configuration;
using pengdows.crud.enums;
using pengdows.crud.FakeDb;
using pengdows.crud.wrappers;
using Xunit;

namespace pengdows.crud.tests;

public class DatabaseContextTests
{
    public static IEnumerable<object[]> AllSupportedProviders() =>
        Enum.GetValues<SupportedDatabase>()
            .Where(p => p != SupportedDatabase.Unknown)
            .Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(AllSupportedProviders))]
    public void CanInitializeContext_ForEachSupportedProvider(SupportedDatabase product)
    {
        var factory = new FakeDbFactory(product);
        var config = new DatabaseContextConfiguration
        {
            DbMode = DbMode.SingleWriter,
            ProviderName = product.ToString(),
            ConnectionString = $"Data Source=test;EmulatedProduct={product}"
        };
        var context = new DatabaseContext(config, factory);

        var conn = context.GetConnection(ExecutionType.Read);
        Assert.NotNull(conn);
        Assert.Equal(ConnectionState.Closed, conn.State);
        
        var schema = conn.GetSchema();
        Assert.NotNull(schema);
        Assert.True(schema.Rows.Count > 0);
    }

    // [Fact]
    // public void Constructor_WithNullFactory_Throws()
    // {
    //     Assert.Throws<NullReferenceException>(() =>
    //         new DatabaseContext("fake", (string)null!));
    // }

    [Theory]
    [MemberData(nameof(AllSupportedProviders))]
    public void WrapObjectName_SplitsAndWrapsCorrectly(SupportedDatabase product)
    {
        var factory = new FakeDbFactory(product);
        var context = new DatabaseContext($"Data Source=test;EmulatedProduct={product}", factory);
        var wrapped = context.WrapObjectName("schema.table");
        Assert.Contains(".", wrapped);
    }

    [Theory]
    [MemberData(nameof(AllSupportedProviders))]
    public void GenerateRandomName_ValidatesFirstChar(SupportedDatabase product)
    {
        var factory = new FakeDbFactory(product);
        var context = new DatabaseContext($"Data Source=test;EmulatedProduct={product}", factory);
        var name = context.GenerateRandomName(10);
        Assert.True(char.IsLetter(name[0]));
    }

    [Theory]
    [MemberData(nameof(AllSupportedProviders))]
    public void CreateDbParameter_SetsPropertiesCorrectly(SupportedDatabase product)
    {
        var factory = new FakeDbFactory(product);
        var context = new DatabaseContext($"Data Source=test;EmulatedProduct={product}", factory);
        var result = context.CreateDbParameter("p1", DbType.Int32, 123);

        Assert.Equal("p1", result.ParameterName);
        Assert.Equal(DbType.Int32, result.DbType);
        Assert.Equal(123, result.Value);
    }

    [Theory]
    [MemberData(nameof(AllSupportedProviders))]
    public async Task CloseAndDisposeConnectionAsync_WithAsyncDisposable_DisposesCorrectly(SupportedDatabase product)
    {
        var mockTracked = new Mock<ITrackedConnection>();
        mockTracked.As<IAsyncDisposable>().Setup(d => d.DisposeAsync())
            .Returns(ValueTask.CompletedTask).Verifiable();

        var factory = new FakeDbFactory(product);
        var context = new DatabaseContext($"Data Source=test;EmulatedProduct={product}", factory);
        await context.CloseAndDisposeConnectionAsync(mockTracked.Object);

        mockTracked.As<IAsyncDisposable>().Verify(d => d.DisposeAsync(), Times.Once);
    }

    [Theory]
    [MemberData(nameof(AllSupportedProviders))]
    public void AssertIsWriteConnection_WhenFalse_Throws(SupportedDatabase product)
    {
        var factory = new FakeDbFactory(product);
        var context = new DatabaseContext($"Data Source=test;EmulatedProduct={product}", factory, readWriteMode: ReadWriteMode.ReadOnly);
        Assert.Throws<InvalidOperationException>(() => context.AssertIsWriteConnection());
    }

    [Theory]
    [MemberData(nameof(AllSupportedProviders))]
    public void AssertIsReadConnection_WhenFalse_Throws(SupportedDatabase product)
    {
        var factory = new FakeDbFactory(product);
        var context = new DatabaseContext($"Data Source=test;EmulatedProduct={product}", factory, readWriteMode: ReadWriteMode.WriteOnly);
        Assert.Throws<InvalidOperationException>(() => context.AssertIsReadConnection());
    }
}
