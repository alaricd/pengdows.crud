namespace pengdows.crud;

public enum DbMode
{
    Standard, // uses connection pooling, asking for a new connection each time a statement is executed, unless a transaction is being used.
    SingleConnection, //– funnels everything through a single connection, useful for databases that only allow a single connection.
    SqlCe, //– keeps a single connection open all the time, using it for all write access, while allowing many read-only connections. This prevents the database being unloaded and keeping within the rule of only having a single write connection open.
    SqlExpressUserMode //– The same as “Standard”, however it keeps 1 connection open to prevent unloading of the database. This is useful for the new localDb feature in SQL Express.
}