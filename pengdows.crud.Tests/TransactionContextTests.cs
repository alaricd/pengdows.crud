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
    private IDatabaseContext _dbContext;
    

    private IDatabaseContext CreateContext(SupportedDatabase dbType)
    {
          _dbContext = new DatabaseContext("Data Source=:memory:", SqliteFactory.Instance, new TypeMapRegistry());
        return _dbContext;
    }

    // public TransactionContextTests()
    // {
    //     _mockContext.Setup(c => c.GetConnection(It.IsAny<ExecutionType>(), true)).Returns(_mockConnection.Object);
    //     _mockConnection.Setup(c => c.BeginTransaction(It.IsAny<IsolationLevel>())).Returns(_mockTransaction.Object);
    //     _mockConnection.Setup(c => c.State).Returns(ConnectionState.Open);
    // }

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
        var tx = new TransactionContext(CreateContext(SupportedDatabase.Sqlite));
        tx.Commit();

        Assert.True(tx.WasCommitted);
        Assert.True(tx.IsCompleted);
       
    }

    [Fact]
    public void Rollback_SetsRollbackState()
    {
        var tx = new TransactionContext(_dbContext);
        tx.Rollback();

        Assert.True(tx.WasRolledBack);
        Assert.True(tx.IsCompleted);
         
    }

    [Fact]
    public void Commit_AfterDispose_Throws()
    {
        var tx = new TransactionContext(_dbContext);
        tx.Dispose();

        Assert.Throws<ObjectDisposedException>(() => tx.Commit());
    }

    [Fact]
    public async Task DisposeAsync_Uncommitted_TriggersRollback()
    {
        var tx = new TransactionContext(_dbContext);
        await tx.DisposeAsync();

        Assert.True(tx.IsCompleted);
   }
}