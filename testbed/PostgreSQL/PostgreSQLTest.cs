using pengdows.crud;

namespace testbed;

public class PostgreSQLTest(IDatabaseContext context, IServiceProvider serviceProvider) : TestProvider(context, serviceProvider)
{
    public override async Task CreateTable()

    {
        var databaseContext = context;
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
-- Create table
CREATE TABLE {0}test_table{1} (
    {0}id{1} SERIAL PRIMARY KEY,
    {0}name{1} VARCHAR(100) NOT NULL,
    {0}description{1} VARCHAR(1000) NOT NULL,
    {0}created_at{1} TIMESTAMP NOT NULL,
    {0}created_by{1} VARCHAR(100) NOT NULL,
    {0}updated_at{1} TIMESTAMP NOT NULL,
    {0}updated_by{1} VARCHAR(100) NOT NULL
);
", qp, qs);
        try
        {
            await sqlContainer.ExecuteNonQueryAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message + "\n --- Continuing anyways");
        }
    }
}