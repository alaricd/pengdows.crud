using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Moq;
using pengdows.crud;
using pengdows.crud.enums;
using pengdows.crud.wrappers;
using Xunit;

public class TransactionContextTests
{
    private readonly Mock<IDatabaseContext> _mockContext = new();
    private readonly Mock<IDbTransaction> _mockTransaction = new();
    private readonly Mock<ITrackedConnection> _mockConnection = new();

    private DatabaseContext CreateContext(SupportedDatabase dbType)
    {
        var dbContext = new DatabaseContext("Data Source=:memory:", SqliteFactory.Instance, new TypeMapRegistry());
        return dbContext;
    }

    public TransactionContextTests()
    {
        _mockContext.Setup(c => c.GetConnection(It.IsAny<ExecutionType>(), true)).Returns(_mockConnection.Object);
        _mockConnection.Setup(c => c.BeginTransaction(It.IsAny<IsolationLevel>())).Returns(_mockTransaction.Object);
        _mockConnection.Setup(c => c.State).Returns(ConnectionState.Open);
    }

    // [Theory]
    // [InlineData(input, expected)]
    // public void ShouldReturnCorrectValue(string input, string expected)
    // {
    //     
    // }
    // [Fact]
    [Theory]
    [InlineData(SupportedDatabase.CockroachDb, IsolationLevel.Serializable)]
    [InlineData(SupportedDatabase.Sqlite, IsolationLevel.Serializable)]
    public void Constructor_SetsIsolationLevel_Correctly(SupportedDatabase supportedDatabase, IsolationLevel isolationLevel)
    {
        var tx = new TransactionContext(CreateContext(supportedDatabase), IsolationLevel.ReadUncommitted);
        Assert.Equal(IsolationLevel.ReadCommitted, tx.IsolationLevel); // upgraded due to ReadWrite
    }

    [Fact]
    public void Commit_SetsCommittedState()
    {
        var tx = new TransactionContext(CreateContext("sqlLite"));
        tx.Commit();

        Assert.True(tx.WasCommitted);
        Assert.True(tx.IsCompleted);
        _mockTransaction.Verify(t => t.Commit(), Times.Once);
    }

    [Fact]
    public void Rollback_SetsRollbackState()
    {
        var tx = new TransactionContext(_mockContext.Object);
        tx.Rollback();

        Assert.True(tx.WasRolledBack);
        Assert.True(tx.IsCompleted);
        _mockTransaction.Verify(t => t.Rollback(), Times.Once);
    }

    [Fact]
    public void Commit_AfterDispose_Throws()
    {
        var tx = new TransactionContext(_mockContext.Object);
        tx.Dispose();

        Assert.Throws<ObjectDisposedException>(() => tx.Commit());
    }

    [Fact]
    public async Task DisposeAsync_Uncommitted_TriggersRollback()
    {
        var tx = new TransactionContext(_mockContext.Object);
        await tx.DisposeAsync();

        Assert.True(tx.IsCompleted);
        _mockTransaction.Verify(t => t.Rollback(), Times.Once);
    }
}