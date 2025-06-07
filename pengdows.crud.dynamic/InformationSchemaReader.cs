using pengdows.crud.enums;
using WebApplication1.Generator;
using System.Data;
using pengdows.crud.dynamic.DynamicPocoGenerator;

namespace pengdows.crud.dynamic;

public class InformationSchemaReader : ISchemaReader
{
    private readonly IDatabaseContext _context;

    public InformationSchemaReader(IDatabaseContext context)
    {
        _context = context;
    }


    public async Task<List<TableDef>> ReadSchemaAsync()
    {
        var tables = new Dictionary<string, TableDef>(StringComparer.OrdinalIgnoreCase);

        await BuildTableListAsync(tables, CancellationToken.None);


        await BuildColumns(tables);

        return tables.Values.ToList();
    }

    public async Task BuildColumns(Dictionary<string, TableDef> tables)
    {
        using var cmdColumns = _context.CreateSqlContainer();
        cmdColumns.Query.AppendFormat(
            @"SELECT {0}table_schema{1}, 
{0}table_name{1}, 
{0}column_name{1}, 
{0}data_type{1}, 
{0}numeric_precision{1}, 
{0}numeric_scale{1} 
FROM {0}information_schema{1}.{0}columns{1}",
            _context.QuotePrefix,
            _context.QuoteSuffix
        );
        using var readerColumns = await cmdColumns.ExecuteReaderAsync();
        while (await readerColumns.ReadAsync().ConfigureAwait(false))
        {
            var schema = readerColumns.GetString(0);
            var table = readerColumns.GetString(1);
            var key = $"{schema}.{table}";
            if (!tables.TryGetValue(key, out var tableDef))
            {
                continue;
            }

            var col = new ColumnDef
            {
                Name = readerColumns.GetString(2),
                DbType = PocoCompilerService.MapToDbType(_context.Product, readerColumns.GetString(3),
                    readerColumns.IsDBNull(4) ? null : readerColumns.GetInt32(4),
                    readerColumns.IsDBNull(5) ? null : readerColumns.GetInt32(5)) ?? DbType.Object,
                Precision = readerColumns.IsDBNull(4) ? null : readerColumns.GetInt32(4),
                Scale = readerColumns.IsDBNull(5) ? null : readerColumns.GetInt32(5),
            };
            col.Type = "string"; // default, could enhance based on DbType

            tableDef.Columns.Add(col);
        }
    }

    public async Task BuildTableListAsync(Dictionary<string, TableDef> tables, CancellationToken cancellationToken)
    {
        await using var cmd = _context.CreateSqlContainer();
        var q0 = _context.QuotePrefix;
        var q1 = _context.QuoteSuffix;

        switch (_context.Product)
        {
            case SupportedDatabase.SqlServer:
                cmd.Query.Append($@"
SELECT {0}table_schema{1}, {0}table_name{1} 
FROM {0}information_schema{1}.{0}tables{1} 
WHERE {0}table_type{1} = 'BASE TABLE'");
                break;

            case SupportedDatabase.MySql:
            case SupportedDatabase.MariaDb:
                cmd.Query.Append($@"
SELECT {0}table_schema{1}, {0}table_name{1}
FROM {0}information_schema{1}.{0}tables{1}
WHERE {0}table_type{1} = 'BASE TABLE'
  AND {0}table_schema{1} NOT IN ('information_schema', 'mysql', 'performance_schema')");
                break;

            case SupportedDatabase.PostgreSql:
            case SupportedDatabase.CockroachDb:
                cmd.Query.Append($@"
SELECT {0}table_schema{1}, {0}table_name{1}
FROM {0}information_schema{1}.{0}tables{1}
WHERE {0}table_type{1} = 'BASE TABLE'
  AND {0}table_schema{1} NOT IN ('pg_catalog', 'information_schema')");
                break;

            case SupportedDatabase.Oracle:
                cmd.Query.Append($@"
SELECT {0}owner{1} AS {0}table_schema{1}, {0}table_name{1}
FROM {0}all_tables{1}");
                break;

            case SupportedDatabase.Sqlite:
                cmd.Query.Append($@"
SELECT '' AS {0}table_schema{1}, {0}name{1} AS {0}table_name{1}
FROM {0}sqlite_master{1}
WHERE {0}type{1} = 'table'");
                break;

            case SupportedDatabase.Firebird:
                cmd.Query.Append($@"
SELECT '' AS {0}table_schema{1}, {0}rdb$relation_name{1} AS {0}table_name{1}
FROM {0}rdb$relations{1}
WHERE {0}rdb$view_blr{1} IS NULL AND {0}rdb$system_flag{1} = 0");
                break;

            default:
                throw new NotSupportedException($"Unsupported DB: {_context.Product}");
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            tables[$"{schema}.{name}"] = new TableDef { Schema = schema, Name = name };
        }
    }
    // This assumes you have a ColumnDef class that can hold
// OrdinalPosition, IsPrimaryKey, PrimaryKeyOrder, IsAutoIncrement, etc.

    public async Task BuildColumnsAsync(Dictionary<string, TableDef> tables, CancellationToken cancellationToken)
    {
        await using var cmd = _context.CreateSqlContainer();
        switch (_context.Product)
        {
            case SupportedDatabase.SqlServer:
            case SupportedDatabase.PostgreSql:
            case SupportedDatabase.CockroachDb:
            case SupportedDatabase.MySql:
            case SupportedDatabase.MariaDb:
                BuildColumnQueryForMost(cmd);
                break;

            case SupportedDatabase.Oracle:
                BuildColumnQueryForOracle(cmd);
                break;

            case SupportedDatabase.Sqlite:
                throw new NotSupportedException("SQLite requires per-table introspection using PRAGMA table_info.");

            case SupportedDatabase.Firebird:
                BuildColumnQueryForFirebird(cmd);
                break;

            default:
                throw new NotSupportedException($"Unsupported DB: {_context.Product}");
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        //DbDataReaderExtensions.GetColumnSchema()
        DataReaderMapper.LoadObjectsFromDataReaderAsync<ColumnDef>(reader, cancellationToken);
        // while (await reader.ReadAsync())
        // {
        //     var schema = reader.GetString(0);
        //     var table = reader.GetString(1);
        //     var key = $"{schema}.{table}";
        //     if (!tables.TryGetValue(key, out var tableDef))
        //     {
        //         continue;
        //     }
        //
        //     var columnName = reader.GetString(2);
        //     var ordinal = reader.GetInt32(3);
        //     var dbTypeName = reader.GetString(4);
        //     var precision =( reader.IsDBNull(5) ? null : reader.GetInt32(5)) as int?;
        //     var scale = reader.IsDBNull(6) ? null : reader.GetInt32(6);
        //     var isIdentity = !reader.IsDBNull(7) && Convert.ToBoolean(reader.GetValue(7));
        //     var pkOrdinal = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8);
        //
        //     var col = new ColumnDef
        //     {
        //         Name = columnName,
        //         Ordinal = ordinal,
        //         DbType = PocoCompilerService.MapToDbType(_context.Product, dbTypeName, precision, scale) ??
        //                  DbType.Object,
        //         Precision = precision,
        //         Scale = scale,
        //         IsAutoIncrement = isIdentity,
        //         IsPrimaryKey = pkOrdinal.HasValue,
        //         PrimaryKeyOrder = pkOrdinal,
        //         Insertable = !isIdentity,
        //         IsIdCandidate = IsLikelyIdColumn(columnName, table),
        //         IsCreatedOn = IsCreatedOnColumn(columnName),
        //         IsCreatedBy = IsCreatedByColumn(columnName),
        //         IsUpdatedOn = IsUpdatedOnColumn(columnName),
        //         IsUpdatedBy = IsUpdatedByColumn(columnName)
        //     };
        //
        //     tableDef.Columns.Add(col);
        // }
    }

    public void BuildColumnQueryForFirebird(ISqlContainer cmd)
    {
        var qp = _context.QuotePrefix;
        var qs = _context.QuoteSuffix;

        var pk = cmd.AddParameterWithValue(DbType.String, "PRIMARY_KEY");
        var sf = cmd.AddParameterWithValue(DbType.Int32, 0);
        cmd.Query.AppendFormat($@"
SELECT 
    '' AS {0}table_schema{1},
    {0}rf{1}.{0}rdb$relation_name{1} AS {0}table_name{1},
    {0}rf{1}.{0}rdb$field_name{1} AS {0}column_name{1},
    {0}rf{1}.{0}rdb$field_position{1} + 1 AS {0}ordinal_position{1},
    {0}f{1}.{0}rdb$field_type AS {0}data_type{1},
    {0}f{1}.{0}rdb$field_precision{1} AS {0}numeric_precision{1},
    {0}f{1}.{0}rdb$field_scale{1} AS {0}numeric_scale{1},
    CASE WHEN {0}rf{1}.{0}rdb$identity_type{1} IS NOT NULL THEN 1 ELSE 0 END AS {0}is_identity{1},
    {0}pk{1}.{0}rdb$field_position{1} + 1 AS {0}pk_ordinal{1}
FROM {0}db$relation_fields{1} {0}rf{1}
JOIN {0}rdb$fields{1} {0}f{1} ON {0}rf{1}.{0}rdb$field_source{1} = {0}f{1}.{0}rdb$field_name{1}
LEFT JOIN (
  SELECT {0}i{1}.{0}rdb$relation_name{1}, {0}s{1}.{0}rdb$field_name{1}, {0}s{1}.{0}rdb$field_position{0}
  FROM {0}rdb$indices{1} {0}i{1}
  JOIN {0}rdb$index_segments{1} {0}s{1} ON {0}i{1}.{0}rdb$index_name{1} =  {0}s{1}.{0}rdb$index_name{1}
  JOIN {0}rdb$relation_constraints{1} {0}c{1} ON {0}c{1}.{0}rdb$index_name{1} = {0}i{1}.{0}rdb$index_name{1}
  WHERE {0}c{1}.{0}rdb$constraint_type{1} = {2}
) pk
  ON {0}pk{1}.{0}rdb$relation_name{1} = {0}rf{1}.{0}rdb$relation_name{1} AND {0}pk{1}.{0}rdb$field_name{1} = {0}rf{1}.{0}rdb$field_name{1}
WHERE {0}rf{1}.{0}rdb$system_flag{1} = {3}", qp, qs,
            _context.MakeParameterName(pk),
            _context.MakeParameterName(sf));
    }

    public void BuildColumnQueryForMost(ISqlContainer sqlContainer)
    {
        var qp = _context.QuotePrefix;
        var qs = _context.QuoteSuffix;
        var pk = sqlContainer.AddParameterWithValue(DbType.String, "PRIMARY_KEY");
        string identityExpr = _context.Product switch
        {
            SupportedDatabase.MySql or SupportedDatabase.MariaDb =>
                $"CASE WHEN c.{0}extra{1} LIKE '%auto_increment%' THEN 1 ELSE 0 END",
            _ => $"c.{0}is_identity{1}"
        };

        sqlContainer.Query.Append($@"
SELECT 
    {0}c{1}.{0}table_schema{1},
    {0}c{1}.{0}table_name{1},
    {0}c{1}.{0}column_name{1},
    {0}c{1}.{0}ordinal_position{1},
    {0}c{1}.{0}data_type{1},
    {0}c{1}.{0}numeric_precision{1},
    {0}c{1}.{0}numeric_scale{1},
    {0}c{1}.{identityExpr} AS is_identity,
    {0}c{1}.{0}ordinal_position{1} AS pk_ordinal
FROM {0}information_schema{1}.{0}columns{1} c
LEFT JOIN {0}information_schema{1}.{0}key_column_usage{1} k
  ON {0}c{1}.{0}table_schema{1} = {0}k{1}.{0}table_schema{1}
 AND {0}c{1}.{0}table_name{1} = {0}k{1}.{0}table_name{1}
 AND {0}c{1}.{0}column_name{1} = {0}k{1}.{0}column_name{1}
LEFT JOIN {0}information_schema{1}.{0}table_constraints{1} {0}tc{1}
  ON {0}k{1}.{0}constraint_name{1} = {0}tc{1}.{0}constraint_name{1}
 AND {0}tc{1}.{0}constraint_type{1} = ");
        sqlContainer.Query.Append(sqlContainer.MakeParameterName(pk));
        return;
    }

    public void BuildColumnQueryForOracle(ISqlContainer sqlContainer)
    {
        var qp = _context.QuotePrefix;
        var qs = _context.QuoteSuffix;
        var constraintParameter = sqlContainer.AddParameterWithValue(DbType.String, "P");
        sqlContainer.Query.AppendFormat($@"
SELECT 
    {0}c{1}.{0}OWNER{1} AS table_schema,
    {0}c{1}.{0}TABLE_NAME{1},
    {0}c{1}.{0}COLUMN_NAME{1},
    {0}c{1}.{0}COLUMN_ID{1} AS ordinal_position,
    {0}c{1}.{0}DATA_TYPE{1},
    {0}c{1}.{0}DATA_PRECISION{1},
    {0}c{1}.{0}DATA_SCALE{1},
    CASE WHEN {0}c{1}.{0}IDENTITY_COLUMN{1} = 'YES' THEN 1 ELSE 0 END AS {0}is_identity{1},
    cc.{0}POSITION{1} AS pk_ordinal
    FROM {0}all_tab_columns{1} {0}c{1}
        LEFT JOIN {0}all_cons_columns{1} cc
        ON {0}c{1}.{0}OWNER{1} = {0}cc{1}.{0}OWNER{1} 
        AND {0}c{1}.{0}TABLE_NAME{1} = {0}cc{1}.{0}TABLE_NAME{1} 
        AND {0}c{1}.{0}COLUMN_NAME{1} = {0}cc{1}.{0}COLUMN_NAME{1}
    LEFT JOIN {0}all_constraints{1} {0}con{1}
        ON  {0}cc{1}.{0}OWNER{1} = {0}con{1}.{0}OWNER{1} 
       AND {0}cc{1}.{0}CONSTRAINT_NAME{1} = {0}con{1}.{0}CONSTRAINT_NAME{1} 
       AND {0}con{1}.{0}CONSTRAINT_TYPE{1} = ");
        sqlContainer.Query.Append(sqlContainer.MakeParameterName(constraintParameter));
    }

    private static bool IsLikelyIdColumn(string columnName, string tableName)
    {
        var norm = columnName.ToLowerInvariant();
        return norm == "id"
               || norm == "rowid"
               || norm == "unid"
               || norm == tableName.ToLowerInvariant() + "_id";
    }

    private static bool IsCreatedOnColumn(string name)
    {
        var norm = name.ToLowerInvariant();
        return norm.Contains("create") && (norm.Contains("on") || norm.Contains("at"));
    }

    private static bool IsCreatedByColumn(string name)
    {
        var norm = name.ToLowerInvariant();
        return norm.Contains("create") && norm.Contains("by");
    }

    private static bool IsUpdatedOnColumn(string name)
    {
        var norm = name.ToLowerInvariant();
        return norm.Contains("update") && (norm.Contains("on") || norm.Contains("at"));
    }

    private static bool IsUpdatedByColumn(string name)
    {
        var norm = name.ToLowerInvariant();
        return norm.Contains("update") && norm.Contains("by");
    }
}