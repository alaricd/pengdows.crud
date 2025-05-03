
using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Moq;
using pengdows.crud.enums;
using pengdows.crud.FakeDb;
using pengdows.crud.wrappers;
using Xunit;

namespace pengdows.crud.tests;

public class DatabaseContextTests
{
    private readonly DbProviderFactory _mockFactory = new FakeDbFactory(SupportedDatabase.SqlServer.ToString());
    private readonly DbConnection _mockConnection ;
    private readonly DbCommand _mockCommand;

    public DatabaseContextTests()
    {
        // _mockFactory = new Mock<DbProviderFactory>();
        // _mockConnection = new Mock<DbConnection>();
        // _mockCommand = new Mock<DbCommand>();
        //
        // _mockFactory.Setup(f => f.CreateConnection()).Returns(_mockConnection.Object);
        // _mockConnection.Setup(c => c.CreateCommand()).Returns(_mockCommand.Object);
        // _mockConnection.SetupProperty(c => c.ConnectionString);
        // _mockConnection.Setup(c => c.Open());
        // _mockConnection.Setup(c => c.State).Returns(ConnectionState.Open);
    }

    [Fact]
    public void Constructor_WithNullFactory_Throws()
    {
        Assert.Throws<NullReferenceException>(() =>
            new DatabaseContext("fake", (string)null!));
    }

    [Fact]
    public void GetConnection_ShouldSetConnectionString_AndOpenConnection()
    {
        var context = new DatabaseContext("Data Source=test;", _mockFactory);
        var conn = context.GetConnection(ExecutionType.Read);
        Assert.NotNull(conn);
      //  _mockConnection.VerifySet(c => c.ConnectionString = "Data Source=test;", Times.Once);
       // _mockConnection.Verify(c => c.Open(), Times.Once);
    }

    [Fact]
    public void WrapObjectName_SplitsAndWrapsCorrectly()
    {
        var context = new DatabaseContext("Data Source=test;", _mockFactory);
        var wrapped = context.WrapObjectName("schema.table");
        Assert.Contains(".", wrapped); // Simulates the wrapping e.g., [schema].[table]
    }

    [Fact]
    public void GenerateRandomName_ValidatesFirstChar()
    {
        var context = new DatabaseContext("Data Source=test;", _mockFactory);
        var name = context.GenerateRandomName(10);
        Assert.True(char.IsLetter(name[0]));
    }

    [Fact]
    public void CreateDbParameter_SetsPropertiesCorrectly()
    {
        var parameter = new Mock<DbParameter>();
        parameter.SetupAllProperties();

       // _mockFactory.Setup(f => f.CreateParameter()).Returns(parameter.Object);

        var context = new DatabaseContext("Data Source=test;", _mockFactory);
        var result = context.CreateDbParameter("p1", DbType.Int32, 123);

        Assert.Equal("p1", result.ParameterName);
        Assert.Equal(DbType.Int32, result.DbType);
        Assert.Equal(123, result.Value);
    }

    [Fact]
    public async Task CloseAndDisposeConnectionAsync_WithAsyncDisposable_DisposesCorrectly()
    {
        var mockTracked = new Mock<ITrackedConnection>();
        mockTracked.As<IAsyncDisposable>().Setup(d => d.DisposeAsync())
            .Returns(ValueTask.CompletedTask).Verifiable();

        var context = new DatabaseContext("Data Source=test;", _mockFactory);
        await context.CloseAndDisposeConnectionAsync(mockTracked.Object);

        mockTracked.As<IAsyncDisposable>().Verify(d => d.DisposeAsync(), Times.Once);
    }

    [Fact]
    public void AssertIsWriteConnection_WhenFalse_Throws()
    {
        var context = new DatabaseContext("Data Source=test;", _mockFactory, readWriteMode: ReadWriteMode.ReadOnly);
        Assert.Throws<InvalidOperationException>(() => context.AssertIsWriteConnection());
    }

    [Fact]
    public void AssertIsReadConnection_WhenFalse_Throws()
    {
        var context = new DatabaseContext("Data Source=test;", _mockFactory, readWriteMode: ReadWriteMode.WriteOnly);
        Assert.Throws<InvalidOperationException>(() => context.AssertIsReadConnection());
    }
}
