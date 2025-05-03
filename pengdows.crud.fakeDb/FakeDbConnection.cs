#region

using System.Data;
using System.Data.Common;

#endregion

namespace pengdows.crud.FakeDb;

public class FakeDbConnection : DbConnection, IDbConnection
{
    private ConnectionState _state;
    public override string ConnectionString { get; set; }
    public override string Database => "FakeDb";
    public override string DataSource => "FakeSource";
    public override string ServerVersion => "1.0";

    public override ConnectionState State => _state;

    public override void ChangeDatabase(string _)
    {
    }

    public override void Close()
    {
        _state = ConnectionState.Closed;
    }

    public override void Open()
    {
        _state = ConnectionState.Open;
    }

    public override Task OpenAsync(CancellationToken _)
    {
        _state = ConnectionState.Open;
        return Task.CompletedTask;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel level)
    {
        return new FakeDbTransaction(this, level);
    }

    protected override DbCommand CreateDbCommand()
    {
        return new FakeDbCommand { Connection = this };
    }
}