using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SirChartsALot.Core.Models;

namespace SirChartsALot.Core.Services;

public class SchemaIntrospectionService : ISchemaIntrospectionService
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SchemaIntrospectionService> _logger;
    private const string CacheKey = "schema_cache";
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);

    public SchemaIntrospectionService(
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<SchemaIntrospectionService> logger)
    {
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SchemaCache> GetSchemaCacheAsync()
    {
        if (_cache.TryGetValue<SchemaCache>(CacheKey, out var cached) && cached != null)
        {
            return cached;
        }

        return await RefreshSchemaInternalAsync();
    }

    public async Task<string> GetHighLevelSchemaAsync()
    {
        var schema = await GetSchemaCacheAsync();
        var sb = new StringBuilder();
        
        _logger.LogInformation("High-level schema requested. Cache contains {TableCount} tables", schema.Tables.Count);
        
        if (schema.Tables.Count == 0)
        {
            _logger.LogWarning("Schema cache is empty!");
            return "No tables found in database. Please check your connection string.";
        }
        
        sb.AppendLine("Available tables in the database:");
        foreach (var table in schema.Tables.Values)
        {
            sb.AppendLine($"- {table.TableName}: {table.Description}");
        }
        
        _logger.LogDebug("Returning high-level schema with tables: {Tables}", 
            string.Join(", ", schema.Tables.Keys));
        
        return sb.ToString();
    }

    public async Task<string> GetDetailedSchemaAsync(string[] tableNames)
    {
        var schema = await GetSchemaCacheAsync();
        var sb = new StringBuilder();
        
        if (schema.Tables.Count == 0)
        {
            _logger.LogWarning("Schema cache is empty - no tables loaded from database");
            return "No schema information available. Database connection may not be configured.";
        }
        
        var foundTables = 0;
        foreach (var tableName in tableNames)
        {
            if (schema.Tables.TryGetValue(tableName.ToUpper(), out var table))
            {
                foundTables++;
                sb.AppendLine($"Table: {table.TableName}");
                sb.AppendLine($"Description: {table.Description}");
                sb.AppendLine("Columns:");
                
                foreach (var column in table.Columns)
                {
                    sb.Append($"  - {column.ColumnName} ({column.DataType}");
                    if (column.MaxLength.HasValue)
                        sb.Append($"({column.MaxLength})");
                    sb.Append(")");
                    
                    if (column.IsPrimaryKey)
                        sb.Append(" [PRIMARY KEY]");
                    if (column.IsForeignKey)
                        sb.Append($" [FK -> {column.ReferencedTable}]");
                    if (!column.IsNullable)
                        sb.Append(" [NOT NULL]");
                    
                    sb.AppendLine();
                }
                
                if (table.SampleValues.Any())
                {
                    sb.AppendLine($"Sample values: {string.Join(", ", table.SampleValues.Take(5))}");
                }
                
                sb.AppendLine();
            }
            else
            {
                _logger.LogWarning("Table {TableName} not found in schema cache", tableName);
            }
        }
        
        if (foundTables == 0)
        {
            sb.AppendLine("WARNING: None of the requested tables were found in the database.");
            sb.AppendLine($"Available tables are: {string.Join(", ", schema.Tables.Values.Select(t => t.TableName).Take(10))}");
            if (schema.Tables.Count > 10)
            {
                sb.AppendLine($"... and {schema.Tables.Count - 10} more tables");
            }
        }
        
        // Add relationships
        var relevantRelationships = schema.Relationships.Values
            .Where(r => tableNames.Contains(r.FromTable, StringComparer.OrdinalIgnoreCase) || 
                       tableNames.Contains(r.ToTable, StringComparer.OrdinalIgnoreCase))
            .ToList();
            
        if (relevantRelationships.Any())
        {
            sb.AppendLine("Relationships:");
            foreach (var rel in relevantRelationships)
            {
                sb.AppendLine($"  - {rel.FromTable}.{rel.FromColumn} -> {rel.ToTable}.{rel.ToColumn}");
            }
        }
        
        return sb.ToString();
    }

    public async Task RefreshSchemaAsync()
    {
        await RefreshSchemaInternalAsync();
    }

    private async Task<SchemaCache> RefreshSchemaInternalAsync()
    {
        _logger.LogInformation("Refreshing database schema cache");
        
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("No database connection string configured. Please add ConnectionStrings:DefaultConnection to your appsettings.");
        }

        var schemaCache = new SchemaCache
        {
            LastRefreshed = DateTime.UtcNow
        };

        try
        {
            using var connection = new SqlConnection(connectionString);
            _logger.LogInformation("Opening connection to database...");
            await connection.OpenAsync();
            _logger.LogInformation("Connected to database successfully");
            
            // Get all tables
            _logger.LogInformation("Loading table list...");
            var tables = await GetTablesAsync(connection);
            _logger.LogInformation("Found {TableCount} tables in database", tables.Count);
            
            // Get columns for each table
            _logger.LogInformation("Loading column information for each table...");
            foreach (var table in tables)
            {
                table.Columns = await GetColumnsAsync(connection, table.TableName);
                schemaCache.Tables[table.TableName.ToUpper()] = table;
            }
            _logger.LogInformation("Loaded column information for all tables");
            
            // Get foreign key relationships
            _logger.LogInformation("Loading foreign key relationships...");
            schemaCache.Relationships = await GetRelationshipsAsync(connection);
            _logger.LogInformation("Found {RelationshipCount} relationships", schemaCache.Relationships.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing schema from database");
            throw new InvalidOperationException($"Failed to load database schema: {ex.Message}", ex);
        }

        _cache.Set(CacheKey, schemaCache, _cacheExpiration);
        return schemaCache;
    }

    private async Task<List<TableInfo>> GetTablesAsync(SqlConnection connection)
    {
        const string query = @"
            SELECT 
                t.TABLE_SCHEMA,
                t.TABLE_NAME,
                p.value as TABLE_DESCRIPTION
            FROM INFORMATION_SCHEMA.TABLES t
            LEFT JOIN sys.tables st ON st.name = t.TABLE_NAME
            LEFT JOIN sys.extended_properties p ON p.major_id = st.object_id AND p.minor_id = 0 AND p.name = 'MS_Description'
            WHERE t.TABLE_TYPE = 'BASE TABLE' 
            AND t.TABLE_SCHEMA NOT IN ('sys', 'guest', 'INFORMATION_SCHEMA')
            ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME";

        var tables = new List<TableInfo>();
        using var command = new SqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            tables.Add(new TableInfo
            {
                Schema = reader.GetString(0),
                TableName = reader.GetString(1),
                Description = reader.IsDBNull(2) ? $"Table {reader.GetString(1)}" : reader.GetString(2)
            });
        }
        
        return tables;
    }

    private async Task<List<ColumnInfo>> GetColumnsAsync(SqlConnection connection, string tableName)
    {
        const string query = @"
            SELECT 
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.IS_NULLABLE,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IS_PRIMARY_KEY,
                CASE WHEN fk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IS_FOREIGN_KEY,
                fk.REFERENCED_TABLE_NAME
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON c.TABLE_NAME = pk.TABLE_NAME AND c.COLUMN_NAME = pk.COLUMN_NAME
            LEFT JOIN (
                SELECT 
                    ku.TABLE_NAME, 
                    ku.COLUMN_NAME,
                    ku2.TABLE_NAME as REFERENCED_TABLE_NAME
                FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku ON rc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku2 ON rc.UNIQUE_CONSTRAINT_NAME = ku2.CONSTRAINT_NAME
            ) fk ON c.TABLE_NAME = fk.TABLE_NAME AND c.COLUMN_NAME = fk.COLUMN_NAME
            WHERE c.TABLE_NAME = @tableName
            ORDER BY c.ORDINAL_POSITION";

        var columns = new List<ColumnInfo>();
        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@tableName", tableName);
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                ColumnName = reader.GetString(0),
                DataType = reader.GetString(1),
                MaxLength = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                IsNullable = reader.GetString(3) == "YES",
                IsPrimaryKey = reader.GetInt32(4) == 1,
                IsForeignKey = reader.GetInt32(5) == 1,
                ReferencedTable = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }
        
        return columns;
    }

    private async Task<Dictionary<string, RelationshipInfo>> GetRelationshipsAsync(SqlConnection connection)
    {
        const string query = @"
            SELECT 
                fk.name AS FK_NAME,
                tp.name AS FROM_TABLE,
                cp.name AS FROM_COLUMN,
                tr.name AS TO_TABLE,
                cr.name AS TO_COLUMN
            FROM sys.foreign_keys fk
            INNER JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
            INNER JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id
            INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            INNER JOIN sys.columns cp ON fkc.parent_column_id = cp.column_id AND fkc.parent_object_id = cp.object_id
            INNER JOIN sys.columns cr ON fkc.referenced_column_id = cr.column_id AND fkc.referenced_object_id = cr.object_id";

        var relationships = new Dictionary<string, RelationshipInfo>();
        using var command = new SqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var fkName = reader.GetString(0);
            relationships[fkName] = new RelationshipInfo
            {
                FromTable = reader.GetString(1),
                FromColumn = reader.GetString(2),
                ToTable = reader.GetString(3),
                ToColumn = reader.GetString(4)
            };
        }
        
        return relationships;
    }

}