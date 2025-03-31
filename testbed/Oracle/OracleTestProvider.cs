using Oracle.ManagedDataAccess.Client;
using pengdows.crud;

namespace testbed;

public class OracleTestProvider(IDatabaseContext context, IServiceProvider serviceProvider)
    : TestProvider(context, serviceProvider)
{
    // public override async Task CountTestRows(DatabaseContext context, EntityHelper<TestTable, int> helper)
    // {
    //     var sc = context.CreateSqlContainer();
    //     sc.Query.AppendFormat("SELECT COUNT(*) FROM {0}", helper.WrappedWrappedTableName);
    //     var count =await  sc.ExecuteScalarAsync<decimal>();
    //     Console.WriteLine($"Count: {count}");
    // }
    public override async Task CreateTable()
    {
        var databaseContext = context;
        var sqlContainer = databaseContext.CreateSqlContainer();
        var qp = databaseContext.DataSourceInfo.QuotePrefix;
        var qs = databaseContext.DataSourceInfo.QuoteSuffix;

        // Drop table if it exists
        sqlContainer.Query.AppendFormat(@"
BEGIN
  EXECUTE IMMEDIATE 'DROP TABLE {0}test_table{1}';
EXCEPTION
  WHEN OTHERS THEN
    IF SQLCODE != -942 THEN
      RAISE;
    END IF;
END;", qp, qs);

        await sqlContainer.ExecuteNonQueryAsync();

        // Drop sequence if it exists
        sqlContainer.Clear();
        sqlContainer.Query.AppendFormat(@"
BEGIN
  EXECUTE IMMEDIATE 'DROP SEQUENCE {0}test_table_seq{1}';
EXCEPTION
  WHEN OTHERS THEN
    IF SQLCODE != -2289 THEN -- ORA-02289: sequence does not exist
      RAISE;
    END IF;
END;", qp, qs);

        await sqlContainer.ExecuteNonQueryAsync();

        // Create table
        sqlContainer.Clear();
        sqlContainer.Query.AppendFormat(@"
CREATE TABLE {0}test_table{1} (
  {0}id{1} NUMBER PRIMARY KEY,
  {0}name{1} VARCHAR2(100) NOT NULL,
  {0}description{1} VARCHAR2(1000) NOT NULL,
  {0}created_at{1} DATE NOT NULL,
  {0}created_by{1} VARCHAR2(100) NOT NULL,
  {0}updated_at{1} DATETIME NOT NULL,
  {0}updated_by{1} VARCHAR(100) NOT NULL
)", qp, qs);
      Console.WriteLine(sqlContainer.Query.ToString());
        await sqlContainer.ExecuteNonQueryAsync();

        // Create sequence
        sqlContainer.Clear();
        sqlContainer.Query.AppendFormat(@"
CREATE SEQUENCE {0}test_table_seq{1}
START WITH 1
INCREMENT BY 1
NOCACHE
NOCYCLE", qp, qs);

        await sqlContainer.ExecuteNonQueryAsync();

        // Create trigger
        sqlContainer.Clear();
        sqlContainer.Query.AppendFormat(@"
CREATE OR REPLACE TRIGGER {0}test_table_bi{1}
BEFORE INSERT ON {0}test_table{1}
FOR EACH ROW
BEGIN
  IF :NEW.{0}id{1} IS NULL THEN
    SELECT {0}test_table_seq{1}.NEXTVAL
    INTO :NEW.{0}id{1}
    FROM dual;
  END IF;
END;", qp, qs);

        await sqlContainer.ExecuteNonQueryAsync();
    }
}