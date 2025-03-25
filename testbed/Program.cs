// See https://aka.ms/new-console-template for more information


using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using pengdows.crud;
using testbest;

var tmr = new TypeMapRegistry();

var connectionString = "Server=localhost;Port=3306;Database=testdb;User=root;Password=rootpassword;";
var myDb = new DatabaseContext(connectionString, MySqlClientFactory.Instance, tmr);

var sqlConnectionString = "Server=localhost;uid=sa;pwd=YourPassword123;Initial Catalog=testdb;TrustServerCertificate=true";
var sqlDb = new DatabaseContext(sqlConnectionString, SqlClientFactory.Instance, tmr); 

var liteDb = new DatabaseContext("Data Source=mydb.sqlite", SqliteFactory.Instance, tmr);

// var oracleConnctionString = "User Id=system;Password=mysecurepassword; Data Source=localhost:51521/XEPDB1;";
// var oracleDb = new DatabaseContext(oracleConnctionString, OracleClientFactory.Instance,  tmr);
 var pgConnectionString = @"Host=localhost;Port=5432;Username=postgres;Password=mysecretpassword;Database=postgres;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20;Timeout=15;CommandTimeout=30;";
var pgDb = new DatabaseContext(pgConnectionString, NpgsqlFactory.Instance, tmr);
//var oracle = new OracleTestProvider(oracleDb);
var postgres = new PostgreSQLTest(pgDb);
var sql = new TestProvider(sqlDb);
var trash = new TestProvider(myDb);
var lite = new TestProvider(liteDb);
// Console.WriteLine("Testing oracle");
//await oracle.RunTest();
Console.WriteLine("Testing PostgreSQL");
await postgres.RunTest();
Console.WriteLine("Testing mysql");
await trash.RunTest();
Console.WriteLine("Testing sqlite");
await lite.RunTest();
Console.WriteLine("Testing sql server");
await sql.RunTest();
Console.WriteLine("done");