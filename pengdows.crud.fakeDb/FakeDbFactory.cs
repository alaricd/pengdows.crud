#region

using System.Data.Common;

#endregion

namespace pengdows.crud.FakeDb;
 

public sealed class FakeDbFactory : DbProviderFactory
{
    private readonly string _pretendToBe;
    public static readonly FakeDbFactory Instance = new();

    private FakeDbFactory()
    {
        _pretendToBe = "fakeDb";
    }

    public FakeDbFactory(string pretendToBe)
    {
        _pretendToBe = pretendToBe;
    }
    
    public override DbCommand CreateCommand()
    {
        return new FakeDbCommand();
    }

    public override DbConnection CreateConnection()
    {
        return new FakeDbConnection();
    }

    public override DbParameter CreateParameter()
    {
        return new FakeDbParameter();
    }
}
