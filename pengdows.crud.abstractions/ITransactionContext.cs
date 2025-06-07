using System.Data;

namespace pengdows.crud;

public interface ITransactionContext : IDatabaseContext
{
    void Commit();
    void Rollback();
    bool WasCommitted { get; }
    bool WasRolledBack { get; }
    bool IsCompleted { get; }
    IsolationLevel IsolationLevel { get; }
}