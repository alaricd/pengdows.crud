using System.Data.Common;
using WebApplication1.Generator;

namespace pengdows.crud.dynamic.DynamicPocoGenerator;

public interface ISchemaReader
{
    Task<List<TableDef>> ReadSchemaAsync(DbConnection connection);
}