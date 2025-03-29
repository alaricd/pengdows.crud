using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace pengdows.crud.tests;

public class SqlContainerTests : IDisposable
{
    private readonly IDatabaseContext _context;

    public SqlContainerTests()
    {
        // Create an in-memory SQLite connection
        // _connection = new SqliteConnection("Data Source=:memory:");
        // _connection.Open();
        _context = new DatabaseContext("DataSource=:memory:", SqliteFactory.Instance, new TypeMapRegistry(),
            DbMode.SqlExpressUserMode);
    }

    public void Dispose()
    {
    }

    [Fact]
    public void Constructor_WithContext_InitializesQueryEmpty()
    {
        var container = new SqlContainer(_context);
        Assert.NotNull(container.Query);
        Assert.Equal("", container.Query.ToString());
    }

    [Fact]
    public void Constructor_WithQuery_InitializesQueryWithValue()
    {
        var query = "SELECT * FROM Test";
        var container = new SqlContainer(_context, query);
        Assert.Equal(query, container.Query.ToString());
    }

    [Fact]
    public void AppendParameter_GeneratesRandomName_WhenNameIsNull()
    {
        var container = new SqlContainer(_context);
        var param = container.AppendParameter(null, DbType.String, "test");
        Assert.Equal("test", param.Value);
        Assert.Equal(DbType.String, param.DbType);
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_CreatesTable()
    {
        var container = new SqlContainer(_context);
        container.Query.Append("CREATE TABLE Test (Id INTEGER PRIMARY KEY, Name TEXT)");

        var result = await container.ExecuteNonQueryAsync();

        Assert.Equal(0, result); // SQLite returns 0 for DDL statements
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_InsertsData()
    {
        AssertPropertNumerOfConnectionsForMode()
        var container = await BuildTestTable();
        container.Query.Append("INSERT INTO Test (Name) VALUES (@name)");
        container.AppendParameter("@name", DbType.String, "TestName");

        var result = await container.ExecuteNonQueryAsync();

        Assert.Equal(1, result); // 1 row affected
    }

    [Fact]
    public async Task ExecuteScalarAsync_ReturnsValue_WhenRowExists()
    {
        var container = await BuildTestTable();
        AssertPropertNumerOfConnectionsForMode()
        container.Query.Append("INSERT INTO Test (Name) VALUES (@name)");
        container.AppendParameter("name", DbType.String, "TestName");
        await container.ExecuteNonQueryAsync(CommandType.Text);
        container.Clear();
        container.Query.Append("SELECT Name FROM Test WHERE Id = 1");

        var result = await container.ExecuteScalarAsync<string>();

        Assert.Equal("TestName", result);
    }

    [Fact]
    public async Task ExecuteScalarAsync_ThrowsException_WhenNoRows()
    {
        var container = await BuildTestTable();
        container.Query.Append("SELECT Name FROM Test WHERE Id = 1");

        await Assert.ThrowsAsync<Exception>(() => container.ExecuteScalarAsync<string>());
        AssertPropertNumerOfConnectionsForMode();
    }

    private void AssertPropertNumerOfConnectionsForMode()
    {
        switch (_context.ConnectionMode)
        {
            case DbMode.Standard:
                Assert.Equal(1, _context.NumberOfOpenConnections);
                break;
            default:
                Assert.Equal(0, _context.NumberOfOpenConnections);
                break;
        }
    }

    private async Task<ISqlContainer> BuildTestTable()
    {
       var qp = _context.DataSourceInfo.QuotePrefix;
       var qs = _context.DataSourceInfo.QuoteSuffix;
       var sql = string.Format("CREATE TABLE {0}Test{1} ({0}Id{1} INTEGER PRIMARY KEY, {0}Name{1} TEXT)", qp, qs);
        var container = _context.CreateSqlContainer(sql);
        await container.ExecuteNonQueryAsync(CommandType.Text);
        container.Clear();
        return container;
    }

    [Fact]
    public async Task ExecuteReaderAsync_ReturnsData()
    {
        var container = await BuildTestTable();
        var qp = _context.DataSourceInfo.QuotePrefix;
        var qs = _context.DataSourceInfo.QuoteSuffix;
        container.Query.AppendFormat("INSERT INTO {0}Test{1} ({0}Name{1}) VALUES (@name)", qp, qs);
        container.AppendParameter("name", DbType.String, "TestName");
        await container.ExecuteNonQueryAsync(CommandType.Text);
        container.Clear();
        container.Query.AppendFormat("SELECT {0}Name{1} FROM  {0}Test{1}",qp,qs);

        using var reader = await container.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("TestName", reader.GetString(0));
        Assert.False(await reader.ReadAsync());
    }

    // [Theory]
    // [InlineData(ProcWrappingStyle.PostgreSQL, ExecutionType.Read, "SELECT * FROM test_proc")]
    // [InlineData(ProcWrappingStyle.PostgreSQL, ExecutionType.Write, "CALL test_proc")]
    // [InlineData(ProcWrappingStyle.Oracle, ExecutionType.Read, "BEGIN\ntest_proc;\nEND;")]
    // [InlineData(ProcWrappingStyle.Exec, ExecutionType.Write, "EXEC test_proc")]
    // public void WrapForStoredProc_ReturnsCorrectFormat(ProcWrappingStyle style, ExecutionType execType,
    //     string expected)
    // {
    //  var container = _context.CreateSqlContainer();
    //     await container.ExecuteNonQueryAsync(CommandType.Text);
    //       container.Query.Append("test_proc");
    //
    //     var result = container.WrapForStoredProc(execType);
    //
    //     Assert.Equal(expected, result);
    // }
    //
    // [Fact]
    // public void Dispose_ClearsResources()
    // {
    //     var param = container.AppendParameter("@test", DbType.String, "value");
    //     container.Query.Append("SELECT * FROM test");
    //
    //     container.Dispose();
    //
    //     Assert.Equal(0, container.Query.Length);
    // }
}