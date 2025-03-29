using Oracle.ManagedDataAccess.Client;
using pengdows.crud;

namespace testbed;

public class OracleTestProvider(DatabaseContext oracleDb) : TestProvider(oracleDb)
{
    // public override async Task CountTestRows(DatabaseContext context, EntityHelper<TestTable, int> helper)
    // {
    //     var sc = context.CreateSqlContainer();
    //     sc.Query.AppendFormat("SELECT COUNT(*) FROM {0}", helper.WrappedWrappedTableName);
    //     var count =await  sc.ExecuteScalarAsync<decimal>();
    //     Console.WriteLine($"Count: {count}");
    // }
    public override async Task CreateTable(DatabaseContext databaseContext)
    {
        var sqlContainer = databaseContext.CreateSqlContainer();
        var qp = databaseContext.DataSourceInfo.QuotePrefix;
        var qs = databaseContext.DataSourceInfo.QuoteSuffix;
        sqlContainer.Query.AppendFormat(@"BEGIN
  EXECUTE IMMEDIATE 'DROP TABLE {0}test_table{1}';
EXCEPTION
  WHEN OTHERS THEN
    IF SQLCODE != -942 THEN
      RAISE;
    END IF;
END; ", qp, qs);
        try
        {
            await sqlContainer.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (ex.Number == 942)
        {
            // Table did not exist, ignore
        }

        sqlContainer.Query.Clear();
        sqlContainer.Query.AppendFormat(@"
CREATE TABLE {0}test_table{1} (
    {0}id{1} NUMBER(19) ,
    {0}name{1} VARCHAR2(100) NOT NULL,
    {0}created_at{1} TIMESTAMP 
) ", qp, qs);
        try
        {
            Console.WriteLine(sqlContainer.Query);
            await sqlContainer.ExecuteNonQueryAsync();
        }
        catch (OracleException e)
        {
            try
            {
                sqlContainer.Query.Clear();
                sqlContainer.Query.AppendFormat("TRUNCATE TABLE {0}test_table{1};", qp, qs);
                await sqlContainer.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                //eat error quitely if it doesn't support truncate table
            }

            Console.WriteLine(e.Message + "\n --- Continuing anyways");
        }
    }
}