using pengdows.crud;

namespace testbed;

public class TestProvider
{
    private readonly DatabaseContext _context;
    private readonly EntityHelper<TestTable, int> _helper;

    public TestProvider(DatabaseContext databaseContext)
    {
        _context = databaseContext;
        _helper = new EntityHelper<TestTable, int>(databaseContext);
    }


    public async Task RunTest()
    {
        await CreateTable(_context);

        await InsertTestRows(_context, _helper);
        await CountTestRows(_context, _helper);
        var obj = await RetrieveRows(_context, _helper);


        await DeletedRow(_context, _helper);
        Console.WriteLine("Completed testing of provider");
    }

    public virtual async Task CountTestRows(DatabaseContext context, EntityHelper<TestTable, int> helper)
    {
        var sc = _context.CreateSqlContainer();
        sc.Query.AppendFormat("SELECT COUNT(*) FROM {0}", helper.WrappedWrappedTableName);
        var count = await sc.ExecuteScalarAsync<int>();
        Console.WriteLine($"Count: {count}");
    }

    public virtual async Task CreateTable(DatabaseContext databaseContext)
    {
        var sqlContainer = databaseContext.CreateSqlContainer();
        var qp = databaseContext.DataSourceInfo.QuotePrefix;
        var qs = databaseContext.DataSourceInfo.QuoteSuffix;
        sqlContainer.Query.AppendFormat(@"DROP TABLE IF EXISTS {0}test_table{1}", qp, qs);
        try
        {
            await sqlContainer.ExecuteNonQueryAsync();
        }
        catch (Exception ex) //when (ex.Number == 942)
        {
            // Table did not exist, ignore
        }

        sqlContainer.Query.Clear();
        sqlContainer.Query.AppendFormat(@"
CREATE TABLE {0}test_table{1} (
    {0}id{1} BIGINT ,
    {0}name{1} VARCHAR(100) NOT NULL,
    {0}created_at{1} DATETIME 
); ", qp, qs);
        try
        {
            await sqlContainer.ExecuteNonQueryAsync();
        }
        catch (Exception e)
        {
            try
            {
                sqlContainer.Query.Clear();
                sqlContainer.Query.AppendFormat("TRUNCATE TABLE {0}test_table{1}", qp, qs);
                await sqlContainer.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                //eat error quitely if it doesn't support truncate table
            }

            Console.WriteLine(e.Message + "\n --- Continuing anyways");
        }
    }

    private async Task InsertTestRows(DatabaseContext databaseContext, IEntityHelper<TestTable, int> helper)
    {
        var t = new TestTable
        {
            Id = 1,
            Name = NameEnum.Test,
            CreatedAt = DateTime.UtcNow
        };
        var sq = helper.BuildCreate(t);
        await sq.ExecuteNonQueryAsync();
    }

    private async Task<TestTable> RetrieveRows(DatabaseContext databaseContext, IEntityHelper<TestTable, int> helper)
    {
        var sc = helper.BuildRetrieve([1]);

        Console.WriteLine(sc.Query.ToString());

        var x = await helper.LoadSingleAsync(sc);
        return x;
    }

    private async Task DeletedRow(DatabaseContext databaseContext, EntityHelper<TestTable, int> entityHelper)
    {
        var sc = _helper.BuildDelete(1);
        var count = await sc.ExecuteNonQueryAsync();
        if (count != 1) throw new Exception("Delete failed");
    }
}