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
//
// const string connectionString = "Server=localhost;Port=3306;Database=testdb;User=root;Password=rootpassword;";
// var myDb = new DatabaseContext(connectionString, MySqlClientFactory.Instance, host.Services.GetRequiredService<ITypeMapRegistry>());
//
// var sqlConnectionString =
//     "Server=localhost;uid=sa;pwd=YourPassword123;Initial Catalog=testdb;TrustServerCertificate=true";
// var sqlDb = new DatabaseContext(sqlConnectionString, SqlClientFactory.Instance, host.Services.GetRequiredService<ITypeMapRegistry>());
//
var liteDb = new DatabaseContext("Data Source=mydb.sqlite", SqliteFactory.Instance,
    host.Services.GetRequiredService<ITypeMapRegistry>());
var lite = new TestProvider(liteDb, host.Services);
await lite.RunTest();

var my = new MySqlTestContainer();
await my.RunTestWithContainerAsync<TestProvider>(host.Services, (db, sp) => new TestProvider(db, sp));
var maria = new MariaDbContainer();
await maria.RunTestWithContainerAsync<TestProvider>(host.Services, (db, sp) => new TestProvider(db, sp));
var pg = new PostgreSqlTestContainer();
await pg.RunTestWithContainerAsync<PostgreSQLTest>(host.Services, (db, sp) => new PostgreSQLTest(db, sp));
var ms = new SqlServerTestContainer();
await ms.RunTestWithContainerAsync<TestProvider>(host.Services, (db, sp) => new TestProvider(db, sp));
// var o = new OracleTestContainer();
// await o.RunTestWithContainerAsync<OracleTestProvider>( host.Services, (db, sp) => new OracleTestProvider(db, sp));
var fb = new FirebirdSqlTestContainer();
await fb.RunTestWithContainerAsync(host.Services, (db, sp) => new FirebirdTestProvider(db, sp));

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