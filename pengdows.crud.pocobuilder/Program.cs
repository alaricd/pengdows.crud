// using Microsoft.AspNetCore.Builder;
// using Microsoft.AspNetCore.Http;
// using Microsoft.Data.SqlClient;
// using MySql.Data.MySqlClient;
// using Npgsql;
// using Oracle.ManagedDataAccess.Client;
// using Microsoft.Data.Sqlite;
// using pengdows.crud;
//
// // Firebird and CockroachDb require their own NuGet packages and connection types
//
// var builder = WebApplication.CreateBuilder(args);
// //builder.Services.AddKeyedSingleton<>()
// var app = builder.Build();
//
//
// app.MapPost("/schemas/tables", async (DbRequest request) =>
// {
//     // Connection is handled by DatabaseContext
//
//     var sql = request.Provider switch
//     {
//         "SqlServer" => "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'",
//         "PostgreSql" or "CockroachDb" => "SELECT table_schema AS TABLE_SCHEMA, table_name AS TABLE_NAME FROM information_schema.tables WHERE table_type = 'BASE TABLE'",
//         "MySql" or "MariaDb" => "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'",
//         "Oracle" => "SELECT OWNER AS TABLE_SCHEMA, TABLE_NAME FROM ALL_TABLES",
//         "Sqlite" => "SELECT '' AS TABLE_SCHEMA, name AS TABLE_NAME FROM sqlite_master WHERE type='table'",
//         "Firebird" => "SELECT RDB$RELATION_NAME AS TABLE_NAME FROM RDB$RELATIONS WHERE RDB$SYSTEM_FLAG = 0",
//         _ => throw new NotSupportedException($"Unsupported DB type: {request.DbType}")
//     };
//
//     using var db = new DatabaseContext(request.ConnectionString, request.Provider);
//     var tables = await db.QueryAsync<TableInfo>(sql);
//     return Results.Ok(tables);
// });
//
// app.MapPost("/schemas/columns", async (DbTableRequest request) =>
// {
//     using var connection = CreateConnection(request);
//     var sql = request.DbType switch
//     {
//         "SqlServer" => @"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT, CHARACTER_MAXIMUM_LENGTH
//                           FROM INFORMATION_SCHEMA.COLUMNS
//                           WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table",
//         "PostgreSql" or "CockroachDb" => @"SELECT column_name AS COLUMN_NAME, data_type AS DATA_TYPE, is_nullable AS IS_NULLABLE, column_default AS COLUMN_DEFAULT, character_maximum_length AS CHARACTER_MAXIMUM_LENGTH
//                                           FROM information_schema.columns
//                                           WHERE table_schema = @Schema AND table_name = @Table",
//         "MySql" or "MariaDb" => @"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT, CHARACTER_MAXIMUM_LENGTH
//                                 FROM INFORMATION_SCHEMA.COLUMNS
//                                 WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table",
//         "Oracle" => @"SELECT COLUMN_NAME, DATA_TYPE, NULLABLE AS IS_NULLABLE, DATA_DEFAULT AS COLUMN_DEFAULT, DATA_LENGTH AS CHARACTER_MAXIMUM_LENGTH
//                     FROM ALL_TAB_COLUMNS
//                     WHERE OWNER = @Schema AND TABLE_NAME = @Table",
//         "Sqlite" => @"PRAGMA table_info(@Table)",
//         "Firebird" => @"SELECT RDB$FIELD_NAME AS COLUMN_NAME, RDB$FIELD_TYPE AS DATA_TYPE, RDB$NULL_FLAG AS IS_NULLABLE
//                       FROM RDB$RELATION_FIELDS
//                       WHERE RDB$RELATION_NAME = @Table",
//         _ => throw new NotSupportedException($"Unsupported DB type: {request.DbType}")
//     };
//
//     var parameters = new { Schema = request.Schema, Table = request.Table };
//     using var db = new DatabaseContext(request.ConnectionString, request.Provider);
//     var columns = await db.QueryAsync<ColumnInfo>(sql, parameters);
//     return Results.Ok(columns);
// });
//
// app.Run();
//
//
// //
// // record DbRequest(string Provider, string ConnectionString);
// // record DbTableRequest(string Provider, string ConnectionString, string Schema, string Table);
// // record TableInfo(string Table_Schema, string Table_Name);
// // record ColumnInfo(string Column_Name, string Data_Type, string Is_Nullable, string Column_Default, int? Character_Maximum_Length);

Console.WriteLine("hack");