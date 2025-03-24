// See https://aka.ms/new-console-template for more information


using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using pengdows.crud;
using testbest;

var tmr = new TypeMapRegistry();

var connectionString = "Server=localhost;Port=3306;Database=testdb;User=root;Password=rootpassword;";
var mysql = new DatabaseContext(connectionString, MySqlClientFactory.Instance, tmr);

var sqlConnectionString = "Server=localhost;uid=sa;pwd=YourPassword123;Initial Catalog=testdb;TrustServerCertificate=true";
var sqlServer = new DatabaseContext(sqlConnectionString, SqlClientFactory.Instance, tmr); 

var sl = new DatabaseContext("Data Source=mydb.sqlite", SqliteFactory.Instance, tmr);


var sql = new TestProvider(sqlServer);
var trash = new TestProvider(mysql);
var lite = new TestProvider(sl);
Console.WriteLine("Testing mysql");
await trash.RunTest();
//Console.WriteLine("Testing sqlite");
//await lite.RunTest();
Console.WriteLine("Testing sql server");
await sql.RunTest();
Console.WriteLine("done");