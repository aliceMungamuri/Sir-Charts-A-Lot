using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SirChartsALot.Core.Services;

public class SqlExecutionService : ISqlExecutionService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SqlExecutionService> _logger;
    private const int QueryTimeoutSeconds = 30;

    public SqlExecutionService(IConfiguration configuration, ILogger<SqlExecutionService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<SqlQueryResult> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("No database connection string configured. Please add ConnectionStrings:DefaultConnection to your appsettings.");
        }

        var stopwatch = Stopwatch.StartNew();
        var result = new SqlQueryResult();

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            
            using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = QueryTimeoutSeconds;
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            // Get column names
            for (int i = 0; i < reader.FieldCount; i++)
            {
                result.Columns.Add(reader.GetName(i));
            }
            
            // Read all rows
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new object[reader.FieldCount];
                reader.GetValues(row);
                result.Rows.Add(row);
            }
            
            result.TotalRowCount = result.Rows.Count;
            result.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;
            
            _logger.LogInformation("Query executed successfully. Rows: {RowCount}, Time: {Time}ms", 
                result.TotalRowCount, result.ExecutionTimeMs);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL query");
            throw;
        }
    }

    public async IAsyncEnumerable<SqlBatch> StreamResultsAsync(
        string sql, 
        int batchSize = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("No database connection string configured. Please add ConnectionStrings:DefaultConnection to your appsettings.");
        }

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        
        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = QueryTimeoutSeconds;
        
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        // Get column names
        var columns = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }
        
        var currentBatch = new SqlBatch { Columns = columns };
        var totalCount = 0;
        
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            currentBatch.Rows.Add(row);
            totalCount++;
            
            if (currentBatch.Rows.Count >= batchSize)
            {
                currentBatch.TotalRowCount = totalCount;
                yield return currentBatch;
                currentBatch = new SqlBatch { Columns = columns };
            }
        }
        
        // Send final batch
        if (currentBatch.Rows.Count > 0)
        {
            currentBatch.IsComplete = true;
            currentBatch.TotalRowCount = totalCount;
            yield return currentBatch;
        }
    }

}