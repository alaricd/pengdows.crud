// See https://aka.ms/new-console-template for more information


using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Oracle.ManagedDataAccess.Client;
using pengdows.crud;
using testbed;
using testbed.Cockroach;
using testbed.Sybase;

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

await using var sybase = new SybaseTestContainer();
await sybase.RunTestWithContainerAsync(host.Services, (db, sp) => new SybaseTestProvider(db, sp));
//
// await using var cockroach = new CockroachDbTestContainer();
// await cockroach.RunTestWithContainerAsync(host.Services, (db, sp) => new CockroadDbTestProvider(db, sp));
//
// await using (var my = new MySqlTestContainer())
// {
//     await my.RunTestWithContainerAsync<TestProvider>(host.Services, (db, sp) => new TestProvider(db, sp));
// }
//
// var maria = new MariaDbContainer();
// await maria.RunTestWithContainerAsync<TestProvider>(host.Services, (db, sp) => new TestProvider(db, sp));
// var pg = new PostgreSqlTestContainer();
// await pg.RunTestWithContainerAsync<PostgreSQLTestProvider>(host.Services,
//     (db, sp) => new PostgreSQLTestProvider(db, sp));
// var ms = new SqlServerTestContainer();
// await ms.RunTestWithContainerAsync<TestProvider>(host.Services, (db, sp) => new TestProvider(db, sp));
// var o = new OracleTestContainer();
// await o.RunTestWithContainerAsync<OracleTestProvider>(host.Services, (db, sp) => new OracleTestProvider(db, sp));
// var oracleConnectionString = "User Id=system;Password=mysecurepassword; Data Source=localhost:51521/XEPDB1;";
// var oracleDb = new DatabaseContext(oracleConnectionString, OracleClientFactory.Instance,
//     host.Services.GetRequiredService<ITypeMapRegistry>());
// var oracle = new OracleTestProvider(oracleDb, host.Services);
// await oracle.RunTest();
//
//
// var fb = new FirebirdSqlTestContainer();
// await fb.RunTestWithContainerAsync(host.Services, (db, sp) => new FirebirdTestProvider(db, sp));
//

Console.WriteLine("All tests complete.");