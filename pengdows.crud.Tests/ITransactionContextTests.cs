using System.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace pengdows.crud.Tests;

public class ITransactionContextTests
{
    [Fact]
    public void ITransactionContext_DefaultsToExpectedValues()
    {
        using var context =
            new DatabaseContext("Data Source=:memory:",
                SqliteFactory.Instance,
                null,
                DbMode.SingleConnection, ReadWriteMode.ReadWrite);
        using var tx = context.BeginTransaction();
        Assert.Equal(IsolationLevel.ReadCommitted, tx.IsolationLevel);
    }

    [Fact]
    public void ITransactionContext_DefaultsToExpectedValuesForReadOnly()
    {
        using var context =
            new DatabaseContext("Data Source=:memory:",
                SqliteFactory.Instance,
                null,
                DbMode.SingleConnection,
                ReadWriteMode.ReadOnly);
        using var tx = context.BeginTransaction();
        Assert.Equal(IsolationLevel.RepeatableRead, tx.IsolationLevel);
    }
}