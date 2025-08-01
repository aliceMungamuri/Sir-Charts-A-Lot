using SirChartsALot.Core.Models;

namespace SirChartsALot.Core.Services;

public interface ISchemaIntrospectionService
{
    Task<SchemaCache> GetSchemaCacheAsync();
    Task<string> GetHighLevelSchemaAsync();
    Task<string> GetDetailedSchemaAsync(string[] tableNames);
    Task RefreshSchemaAsync();
}