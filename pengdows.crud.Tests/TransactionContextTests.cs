// using System.Data;
// using Xunit;
//
// namespace pengdows.crud.Tests;
//
// public class TransactionContextTests
// {
//     [Fact]
//     public void TransactionContext_DefaultsToRepeatableRead()
//     {
//         var context = new TransactionContext();
//         Assert.Equal(IsolationLevel.RepeatableRead, context.IsolationLevel);
//     }
//
//     [Fact]
//     public void TransactionContext_CanSetRollback()
//     {
//         var context = new TransactionContext();
//         context.RollbackOnly = true;
//         Assert.True(context.RollbackOnly);
//     }
// }

