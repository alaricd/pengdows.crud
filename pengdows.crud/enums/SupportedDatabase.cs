namespace pengdows.crud;

public enum SupportedDatabase
{
    Unknown,
    SqlServer,
    MySql,
    PostgreSql,
    Oracle,
    Sqlite,
    Firebird,
    //Db2, Db2 and Informix can't be supported under modern .net frameworks
    CockroachDb,
    MariaDb
}