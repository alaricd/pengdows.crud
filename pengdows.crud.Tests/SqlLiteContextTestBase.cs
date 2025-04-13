using Microsoft.Data.Sqlite;

namespace pengdows.crud.Tests;

public class SqlLiteContextTestBase

{
    public TypeMapRegistry TypeMap { get; }
    public IDatabaseContext Context { get; }

    protected SqlLiteContextTestBase()
    {
        TypeMap = new TypeMapRegistry();
        Context = new DatabaseContext("Data Source=:memory:", SqliteFactory.Instance, TypeMap);
    }
}