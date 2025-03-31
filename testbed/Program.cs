// See https://aka.ms/new-console-template for more information


using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using pengdows.crud;
using testbed;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddScoped<IAuditContextProvider<string>, StringAuditContextProvider>();
//builder.Services.AddSingleton<IDatabaseContextProvider, LocalDockerDbProvider>();
builder.Services.AddSingleton<ITypeMapRegistry, TypeMapRegistry>();

var host = builder.Build();
var liteDb = new DatabaseContext("Data Source=mydb.sqlite", SqliteFactory.Instance,
    host.Services.GetRequiredService<ITypeMapRegistry>());
var lite = new TestProvider(liteDb, host.Services);
await lite.RunTest();

var my = new MySqlTestContainer();
await my.RunTestWithContainerAsync<TestProvider>(host.Services, (db, sp) => new TestProvider(db, sp));
var pg = new PostgreSqlTestContainer();
await pg.RunTestWithContainerAsync<PostgreSQLTest>(host.Services, (db, sp) => new PostgreSQLTest(db, sp));
var ms = new SqlServerTestContainer();
await ms.RunTestWithContainerAsync<TestProvider>(host.Services, (db, sp) => new TestProvider(db, sp));
// var oracleConnectionString = "User Id=system;Password=mysecurepassword; Data Source=localhost:51521/XEPDB1;";
// var oracleConnectionString =
//     "USER ID=system;PASSWORD=mysecurepassword;DATA SOURCE=\"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=51521))(CONNECT_DATA=(SERVICE_NAME=XEPDB1)))\"";
//  var oracleDb = new DatabaseContext(oracleConnectionString, OracleClientFactory.Instance, host.Services.GetRequiredService<ITypeMapRegistry>());
// var oracle = new OracleTestProvider(oracleDb, host.Services);
//  await oracle.RunTest();

 // var o = new OracleTestContainer();
 // await o.RunTestWithContainerAsync<OracleTestProvider>( host.Services, (db, sp) => new OracleTestProvider(db, sp));

Console.WriteLine("All tests complete.");