namespace SirChartsALot.Core.Services;

public interface ISqlExecutionService
{
    Task<SqlQueryResult> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default);
    IAsyncEnumerable<SqlBatch> StreamResultsAsync(string sql, int batchSize = 100, CancellationToken cancellationToken = default);
}

public class SqlQueryResult
{
    public List<string> Columns { get; set; } = new();
    public List<object[]> Rows { get; set; } = new();
    public int TotalRowCount { get; set; }
    public int ExecutionTimeMs { get; set; }
}

public class SqlBatch
{
    public List<object[]> Rows { get; set; } = new();
    public List<string> Columns { get; set; } = new();
    public bool IsComplete { get; set; }
    public int TotalRowCount { get; set; }
}