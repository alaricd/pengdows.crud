using System.Data;
using pengdows.crud;

namespace testbed;

public class TestProvider:IAsyncTestProvider
{
    private readonly IDatabaseContext _context;
    private readonly EntityHelper<TestTable, long> _helper;
    

    public TestProvider(IDatabaseContext databaseContext, IServiceProvider serviceProvider)
    {
        _context = databaseContext;
        _helper = new EntityHelper<TestTable, long>(databaseContext, serviceProvider);
    }


    public async Task RunTest()
    {
        Console.WriteLine("Completed testing of provider:" + _context.DataSourceInfo.Product.ToString());
        try
        {
            await CreateTable();

            await InsertTestRows();
            await CountTestRows();
            var obj = await RetrieveRows();

            await DeletedRow(obj);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to complete tests successfully: " + ex.Message);
        }
        finally
        {
            Console.WriteLine("Completed testing of provider:" + _context.DataSourceInfo.Product.ToString());
        }
    }

    public virtual async Task CountTestRows()
    {
        var context = _context;

        var sc = _context.CreateSqlContainer();
        sc.Query.AppendFormat("SELECT COUNT(*) FROM {0}", _helper.WrappedTableName);
        var count = await sc.ExecuteScalarAsync<int>();
        Console.WriteLine($"Count: {count}");
    }

    public virtual async Task CreateTable()
    {
        var databaseContext = _context;
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
    {0}id{1} BIGINT  NOT NULL UNIQUE, 
    {0}name{1} VARCHAR(100) NOT NULL,
    {0}description{1} VARCHAR(1000) NOT NULL,
    {0}created_at{1} DATETIME NOT NULL,
    {0}created_by{1} VARCHAR(100) NOT NULL,
    {0}updated_at{1} DATETIME NOT NULL,
    {0}updated_by{1} VARCHAR(100) NOT NULL
    
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

    private async Task InsertTestRows()
    {
        var t = new TestTable
        {
            Id = 1,
            Name = NameEnum.Test,
            Description = "Test Description"
        };
        var sq = _helper.BuildCreate(t);
        await sq.ExecuteNonQueryAsync();
    }

    private async Task<TestTable> RetrieveRows( )
    {
        var sc = _helper.BuildRetrieve([1]);

        Console.WriteLine(sc.Query.ToString());

        var x = await _helper.LoadSingleAsync(sc);
        return x;
    }

    private async Task DeletedRow(TestTable t)
    {
        var sc = _helper.BuildDelete(t.Id);
        var count = await sc.ExecuteNonQueryAsync();
        if (count != 1) throw new Exception("Delete failed");
    }
}