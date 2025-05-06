using System.Data.Common;
using WebApplication1.Generator;

namespace pengdows.crud.dynamic.DynamicPocoGenerator;

public interface ISchemaReader
{
    Task<List<TableDef>> ReadSchemaAsync();
    Task BuildColumns(Dictionary<string, TableDef> tables);
    Task BuildTableListAsync(Dictionary<string, TableDef> tables, CancellationToken cancellationToken);
    Task BuildColumnsAsync(Dictionary<string, TableDef> tables, CancellationToken cancellationToken);
    void BuildColumnQueryForFirebird(ISqlContainer cmd);
    void BuildColumnQueryForMost(ISqlContainer sqlContainer);
    void BuildColumnQueryForOracle(ISqlContainer sqlContainer);
}