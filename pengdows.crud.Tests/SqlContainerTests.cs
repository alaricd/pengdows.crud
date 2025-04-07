using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace pengdows.crud.Tests;

public class SqlContainerTests : IDisposable
{
    private readonly IDatabaseContext _context;

    public SqlContainerTests()
    {
        // Create an in-memory SQLite connection
        // _connection = new SqliteConnection("Data Source=:memory:");
        // _connection.Open();
        _context = new DatabaseContext(
            "DataSource=:memory:",
            SqliteFactory.Instance,
            new TypeMapRegistry(),
            DbMode.SingleConnection);
    }

    public void Dispose()
    {
    }

    [Fact]
    public void Constructor_WithContext_InitializesQueryEmpty()
    {
        var container = _context.CreateSqlContainer();
        Assert.NotNull(container.Query);
        Assert.Equal("", container.Query.ToString());
    }

    [Fact]
    public void Constructor_WithQuery_InitializesQueryWithValue()
    {
        var qp = _context.QuotePrefix;
        var qs = _context.QuoteSuffix;

        var query = $"SELECT * FROM {qp}Test{qs}";
        var container = _context.CreateSqlContainer(query);
        Assert.Equal(query, container.Query.ToString());
    }

    [Fact]
    public void AppendParameter_GeneratesRandomName_WhenNameIsNull()
    {
        var container = _context.CreateSqlContainer();
        var param = container.AppendParameter(null, DbType.String, "test");
        Assert.Equal("test", param.Value);
        Assert.Equal(DbType.String, param.DbType);
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_CreatesTable()
    {
        var qp = _context.QuotePrefix;
        var qs = _context.QuoteSuffix;
        var container = _context.CreateSqlContainer();
        container.Query.AppendFormat("CREATE TABLE {0}Test{1} ({0}Id{1} INTEGER PRIMARY KEY, {0}Name{1} TEXT)", qp, qs);

        var result = await container.ExecuteNonQueryAsync();

        Assert.Equal(0, result); // SQLite returns 0 for DDL statements
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_InsertsData()
    {
        var qp = _context.QuotePrefix;
        var qs = _context.QuoteSuffix;
        var container = await BuildTestTable();
        AssertProperNumberOfConnectionsForMode();
        var p = container.AppendParameter(  DbType.String, "TestName");
        container.Query.AppendFormat("INSERT INTO {0}Test{1} ({0}Name{1}) VALUES ({2})", qp, qs,
            _context.MakeParameterName(p));

        var result = await container.ExecuteNonQueryAsync();

        Assert.Equal(1, result); // 1 row affected
    }

    [Fact]
    public async Task ExecuteScalarAsync_ReturnsValue_WhenRowExists()
    {
        var qp = _context.QuotePrefix;
        var qs = _context.QuoteSuffix;
        var container = await BuildTestTable();
        AssertProperNumberOfConnectionsForMode();
        var p = container.AppendParameter( DbType.String, "TestName");
        container.Query.AppendFormat("INSERT INTO {0}Test{1} ({0}Name{1}) VALUES ({2})", qp, qs,
            _context.MakeParameterName(p));
        await container.ExecuteNonQueryAsync();
        container.Clear();
        container.Query.AppendFormat("SELECT {0}Name{1} FROM {0}Test{1} WHERE {0}Id{1} = 1", qp, qs);

        var result = await container.ExecuteScalarAsync<string>();

        Assert.Equal("TestName", result);
    }

    [Fact]
    public async Task ExecuteScalarAsync_ThrowsException_WhenNoRows()
    {
        var qp = _context.QuotePrefix;
        var qs = _context.QuoteSuffix;
        var container = await BuildTestTable();

        container.Query.AppendFormat("SELECT {0}Name{1} FROM {0}Test{1} WHERE {0}Id{1} = 1", qp, qs);

        await Assert.ThrowsAsync<Exception>(() => container.ExecuteScalarAsync<string>());
        AssertProperNumberOfConnectionsForMode();
    }

    private void AssertProperNumberOfConnectionsForMode()
    {
        switch (_context.ConnectionMode)
        {
            case DbMode.Standard:
                Assert.Equal(0, _context.NumberOfOpenConnections);
                break;
            default:
                Assert.NotEqual(0, _context.NumberOfOpenConnections);
                break;
        }
    }

    private async Task<ISqlContainer> BuildTestTable()
    {
        var qp = _context.QuotePrefix;
        var qs = _context.QuoteSuffix;
        var sql = string.Format("CREATE TABLE {0}Test{1} ({0}Id{1} INTEGER PRIMARY KEY, {0}Name{1} TEXT)", qp, qs);
        var container = _context.CreateSqlContainer(sql);
        await container.ExecuteNonQueryAsync();
        container.Clear();
        return container;
    }

    [Fact]
    public async Task ExecuteReaderAsync_ReturnsData()
    {
        var container = await BuildTestTable();
        AssertProperNumberOfConnectionsForMode();
        var qp = _context.QuotePrefix;
        var qs = _context.QuoteSuffix;
        var p = container.AppendParameter(  DbType.String, "TestName");
        container.Query.AppendFormat("INSERT INTO {0}Test{1} ({0}Name{1}) VALUES ({2})", qp, qs,
            _context.MakeParameterName(p));
        await container.ExecuteNonQueryAsync();
        AssertProperNumberOfConnectionsForMode();
        container.Clear();
        container.Query.AppendFormat("SELECT {0}Name{1} FROM {0}Test{1}", qp, qs);

        await using var reader = await container.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("TestName", reader.GetString(0));
        Assert.False(await reader.ReadAsync());
        AssertProperNumberOfConnectionsForMode();
    }

    [Fact]
    public void BuildCreate_SkipsNonWritableId()
    {
        // Arrange
        var typeMap = new TypeMapRegistry();
        typeMap.Register<IdentityTestEntity>(); // assumes you auto-build TableInfo from attributes

        var helper = new EntityHelper<IdentityTestEntity, int>(_context, null);

        var entity = new IdentityTestEntity { Id = 42, Name = "Hello" };

        // Act
        var container = helper.BuildCreate(entity);
        var sql = container.Query.ToString();

        // Assert
        var columnId = _context.WrapObjectName("Id");
        var columnName = _context.WrapObjectName("Name");
        Assert.DoesNotContain(columnId, sql, StringComparison.OrdinalIgnoreCase); // check it's not in the SQL
        Assert.Contains(columnName, sql, StringComparison.OrdinalIgnoreCase); // check that another field is included
        Assert.StartsWith("INSERT INTO", sql, StringComparison.OrdinalIgnoreCase); // sanity check
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