namespace pengdows.crud;

public interface ITransactionContext : IDatabaseContext
{
    void Commit();
    void Rollback();
}