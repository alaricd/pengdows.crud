namespace pengdows.crud;

public interface ITransactionContext : IDbContext
{
    void Commit();
    void Rollback();
}