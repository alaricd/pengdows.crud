// See https://aka.ms/new-console-template for more information


using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Oracle.ManagedDataAccess.Client;
using pengdows.crud;
using testbed;
using testbed.Cockroach;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddScoped<IAuditContextProvider<string>, StringAuditContextProvider>();
builder.Services.AddSingleton<ITypeMapRegistry, TypeMapRegistry>();

var host = builder.Build();

await using (var liteDb = new DatabaseContext("Data Source=mydb.sqlite", SqliteFactory.Instance,
                 host.Services.GetRequiredService<ITypeMapRegistry>()))
{
    var lite = new TestProvider(liteDb, host.Services);
    await lite.RunTest();
    liteDb.Dispose();
}

await using var cockroach = new CockroachDbTestContainer();
await cockroach.RunTestWithContainerAsync(host.Services, (db, sp) => new CockroadDbTestProvider(db, sp));

// await using (var my = new MySqlTestContainer())
// {
//     await my.RunTestWithContainerAsync<TestProvider>(host.Services, (db, sp) => new TestProvider(db, sp));
// }
//
// var maria = new MariaDbContainer();
// await maria.RunTestWithContainerAsync<TestProvider>(host.Services, (db, sp) => new TestProvider(db, sp));
// var pg = new PostgreSqlTestContainer();
// await pg.RunTestWithContainerAsync<PostgreSQLTest>(host.Services, (db, sp) => new PostgreSQLTest(db, sp));
// var ms = new SqlServerTestContainer();
// await ms.RunTestWithContainerAsync<TestProvider>(host.Services, (db, sp) => new TestProvider(db, sp));
// var o = new OracleTestContainer();
// await o.RunTestWithContainerAsync<OracleTestProvider>(host.Services, (db, sp) => new OracleTestProvider(db, sp));
// var oracleConnectionString = "User Id=system;Password=mysecurepassword; Data Source=localhost:51521/XEPDB1;";
// var oracleDb = new DatabaseContext(oracleConnectionString, OracleClientFactory.Instance, host.Services.GetRequiredService<ITypeMapRegistry>());
// var oracle = new OracleTestProvider(oracleDb, host.Services); 
// await oracle.RunTest();


var fb = new FirebirdSqlTestContainer();
await fb.RunTestWithContainerAsync(host.Services, (db, sp) => new FirebirdTestProvider(db, sp));

// var db2 = new Db2TestContainer();
// await db2.RunTestWithContainerAsync(host.Services, (db, sp) => new Db2TestProvider(db, sp));
// In test setup
// var containers = new List<TestContainer>
// {
// //    new OracleTestContainer(),
//   //  new FirebirdSqlTestContainer(),
//     new MySqlTestContainer(),
//     new SqlServerTestContainer(),
//     new PostgreSqlTestContainer()
// };

//await Task.WhenAll(containers.Select(c => c.StartAsync()));

Console.WriteLine("All tests complete.");