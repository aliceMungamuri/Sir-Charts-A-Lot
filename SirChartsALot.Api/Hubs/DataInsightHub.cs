using Microsoft.AspNetCore.SignalR;
using SirChartsALot.Api.Models;
using SirChartsALot.Api.Services;
using SirChartsALot.Core.Agents;
using SirChartsALot.Core.Models;
using SirChartsALot.Core.Services;
using System.Text.Json;

namespace SirChartsALot.Api.Hubs;

/// <summary>
/// Intelligent data insights hub that handles natural language queries and returns appropriate visualizations
/// </summary>
public class DataInsightHub : Hub
{
    private readonly IDomainExpertAgent _domainExpert;
    private readonly ISqlQueryExpertAgent _sqlExpert;
    private readonly ISqlExecutionService _sqlExecutor;
    private readonly IEnhancedVisualizationService _visualizationService;
    private readonly ISchemaIntrospectionService _schemaIntrospectionService;
    private readonly ILogger<DataInsightHub> _logger;

    public DataInsightHub(
        IDomainExpertAgent domainExpert,
        ISqlQueryExpertAgent sqlExpert,
        ISqlExecutionService sqlExecutor,
        IEnhancedVisualizationService visualizationService,
        ISchemaIntrospectionService schemaIntrospectionService,
        ILogger<DataInsightHub> logger)
    {
        _domainExpert = domainExpert;
        _sqlExpert = sqlExpert;
        _sqlExecutor = sqlExecutor;
        _visualizationService = visualizationService;
        _schemaIntrospectionService = schemaIntrospectionService;
        _logger = logger;
    }

    public async Task SubmitQuery(string query, string sessionId)
    {
        var startTime = DateTime.Now;

        try
        {
            _logger.LogInformation("Received query: {Query} for session: {SessionId}", query, sessionId);

            // Send initial thinking message
            await Clients.Caller.SendAsync("ThinkingUpdate",
                new { message = "Analyzing your query..." });

            // Validate schema is loaded
            var schemaCache = await _schemaIntrospectionService.GetSchemaCacheAsync();
            if (schemaCache.Tables.Count == 0)
            {
                await _schemaIntrospectionService.RefreshSchemaAsync();
                schemaCache = await _schemaIntrospectionService.GetSchemaCacheAsync();
            }

            // Step 1: Domain Analysis
            await SendTimelineEvent("domain-expert", "started", "Analyzing query intent and identifying relevant tables");
            await Clients.Caller.SendAsync("ThinkingUpdate",
                new { message = "Understanding your request..." });

            var domainAnalysis = await _domainExpert.AnalyzeQueryAsync(query);
            _logger.LogInformation("Domain analysis complete. Intent: {Intent}, Tables: {Tables}",
                domainAnalysis.Intent, string.Join(", ", domainAnalysis.Tables));

            await SendTimelineEvent("domain-expert", "completed",
                $"Identified {domainAnalysis.Tables.Count} relevant tables",
                "completed",
                domainAnalysis.Tables.ToArray());

            // Send domain expert analysis details
            await Clients.Caller.SendAsync("DomainAnalysis", new
            {
                tables = domainAnalysis.Tables,
                intent = domainAnalysis.Intent,
                complexity = domainAnalysis.Complexity,
                relationships = domainAnalysis.Relationships,
                details = new[]
                {
                    $"Intent: {domainAnalysis.Intent}",
                    $"Complexity: {domainAnalysis.Complexity}",
                    $"Relationships: {domainAnalysis.Relationships}"
                }
            });

            // Step 2: SQL Generation
            await SendTimelineEvent("sql-expert", "started", "Generating SQL query");
            await Clients.Caller.SendAsync("ThinkingUpdate",
                new { message = "Creating optimized SQL query..." });

            var detailedSchema = await _schemaIntrospectionService.GetDetailedSchemaAsync(domainAnalysis.Tables.ToArray());
            var sqlQuery = await _sqlExpert.GenerateSqlAsync(domainAnalysis, query);
            _logger.LogInformation("Generated SQL: {SqlQuery}", sqlQuery);

            await SendTimelineEvent("sql-expert", "completed", "SQL query generated successfully", "completed");

            // Send SQL to frontend
            var tablesUsed = domainAnalysis.Tables.ToArray();
            var columnsSelected = ExtractColumnsFromQuery(sqlQuery);
            await Clients.Caller.SendAsync("SqlGenerated",
                new SqlGeneratedMessage(sqlQuery, tablesUsed, columnsSelected, false));

            // Step 3: Execute Query
            await SendTimelineEvent("sql-executor", "started", "Executing SQL query");
            await Clients.Caller.SendAsync("ThinkingUpdate",
                new { message = "Retrieving data..." });

            var allData = new List<object>();
            var columns = new List<string>();
            var isFirstBatch = true;

            await foreach (var batch in _sqlExecutor.StreamResultsAsync(sqlQuery))
            {
                if (isFirstBatch)
                {
                    columns = batch.Columns;
                    isFirstBatch = false;
                }
                
                // Convert rows to dynamic objects for JSON serialization
                var dataObjects = batch.Rows.Select(row => 
                {
                    var obj = new Dictionary<string, object>();
                    for (int i = 0; i < columns.Count; i++)
                    {
                        obj[columns[i]] = row[i] ?? DBNull.Value;
                    }
                    return obj;
                }).ToList();
                
                allData.AddRange(dataObjects);
            }

            _logger.LogInformation("Query execution complete. Rows: {RowCount}, Columns: {ColumnCount}",
                allData.Count, columns.Count);

            await SendTimelineEvent("sql-executor", "completed",
                $"Retrieved {allData.Count} rows", "completed");

            // Step 4: Generate Unified Visualization
            await SendTimelineEvent("viz-agent", "started", "Determining optimal visualization");
            await Clients.Caller.SendAsync("ThinkingUpdate",
                new { message = "Analyzing data patterns and selecting best visualization type...", stage = "creating_visualization", progressPercentage = 85 });

            // Get recommendation with sample data (20 rows for AI analysis)
            var sampleData = allData.Take(20).ToList();
            var recommendation = await _visualizationService.GetRecommendationAsync(
                query,
                sqlQuery,
                columns,
                sampleData
            );
            
            // Build visualization with FULL data
            var visualization = _visualizationService.BuildVisualizationFromRecommendation(
                recommendation,
                columns,
                allData  // Use ALL data for building the actual visualization
            );

            _logger.LogInformation("Visualization determined: {Type} with confidence {Confidence}",
                visualization.ResponseType, visualization.Confidence);

            await SendTimelineEvent("viz-agent", "completed",
                $"Selected {visualization.ResponseType} visualization", "completed");

            // Complete thinking
            var duration = (int)(DateTime.Now - startTime).TotalSeconds;
            await Clients.Caller.SendAsync("ThinkingComplete", new ThinkingCompleteMessage(duration));

            // Send appropriate visualization message based on type
            await SendVisualizationResponse(visualization, columns, allData);

            // Send summary message
            var summaryMessage = GenerateSummaryMessage(visualization, allData.Count);
            await Clients.Caller.SendAsync("ReceiveMessage", summaryMessage);

            _logger.LogInformation("Successfully processed query end-to-end");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query: {Query}", query);

            await SendTimelineEvent("system", "error", "Error processing query", "error",
                new[] { ex.Message });

            await Clients.Caller.SendAsync("ThinkingComplete", new ThinkingCompleteMessage(0));

            await Clients.Caller.SendAsync("ReceiveMessage",
                $"I apologize, but I encountered an error processing your request: {ex.Message}");
        }
    }

    private async Task SendVisualizationResponse(UnifiedVisualizationResponse visualization, List<string> columns, List<object> data)
    {
        // Data is already populated by the visualization service in the new architecture
        // No need to populate data here anymore

        var serializedConfig = JsonSerializer.Serialize(visualization, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Send unified visualization message
        await Clients.Caller.SendAsync("UnifiedVisualizationReady",
            new UnifiedVisualizationMessage(
                visualization.ResponseType,
                GetVisualizationTitle(visualization),
                visualization.Confidence,
                visualization.SelectionReasoning,
                serializedConfig,
                columns.ToArray(),
                data.Count
            ));

        // Send additional messages based on response type
        switch (visualization.ResponseType)
        {
            case ResponseType.Chart:
                // For backward compatibility, also send traditional visualization message
                //if (visualization.ChartConfig != null)
                //{
                //    var chartJson = JsonSerializer.Serialize(visualization.ChartConfig.ApexChartOptions);
                //    await Clients.Caller.SendAsync("VisualizationReady",
                //        new VisualizationMessage(
                //            visualization.ChartConfig.ChartType.ToString().ToLower(),
                //            visualization.ChartConfig.ApexChartOptions.Title?.Text ?? "Data Visualization",
                //            chartJson,
                //            columns.ToArray()
                //        ));
                //}
                break;

            case ResponseType.Table:
                // Send table data in batches
                if (visualization.TableConfig != null)
                {
                    await SendTableData(visualization.TableConfig, data);
                }
                break;

            case ResponseType.Text:
                // Send text response
                if (visualization.TextConfig != null)
                {
                    await Clients.Caller.SendAsync("TextResponse",
                        new TextResponseMessage(
                            visualization.TextConfig.Content,
                            visualization.TextConfig.FormatType,
                            visualization.TextConfig.IsSingleValue,
                            visualization.TextConfig.SingleValueMetadata,
                            visualization.TextConfig.Highlights
                        ));
                }
                break;

            case ResponseType.Mixed:
                // Send mixed response components
                if (visualization.TextConfig != null)
                {
                    await Clients.Caller.SendAsync("TextResponse",
                        new TextResponseMessage(
                            visualization.TextConfig.Content,
                            visualization.TextConfig.FormatType,
                            visualization.TextConfig.IsSingleValue,
                            visualization.TextConfig.SingleValueMetadata,
                            visualization.TextConfig.Highlights
                        ));
                }
                if (visualization.SecondaryVisualizations != null)
                {
                    foreach (var secondary in visualization.SecondaryVisualizations.OrderBy(v => v.Order))
                    {
                        // Send secondary visualizations
                        await Clients.Caller.SendAsync("SecondaryVisualization", secondary);
                    }
                }
                break;
        }
    }

    private async Task SendTableData(TableVisualization tableConfig, List<object> data)
    {
        var pageSize = tableConfig.PageSize;
        var totalPages = (int)Math.Ceiling(data.Count / (double)pageSize);

        for (int page = 1; page <= totalPages; page++)
        {
            var pageData = data
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToArray();

            await Clients.Caller.SendAsync("TableData",
                new TableDataMessage(
                    pageData,
                    tableConfig.Columns.ToArray(),
                    page == totalPages,
                    data.Count,
                    page,
                    pageSize
                ));

            // Small delay between pages for streaming effect
            if (page < totalPages)
            {
                await Task.Delay(100);
            }
        }
    }

    private async Task SendTimelineEvent(string agent, string stage, string message, string status = "in_progress", string[]? details = null)
    {
        await Clients.Caller.SendAsync("TimelineEvent",
            new TimelineEventMessage(
                Guid.NewGuid().ToString(),
                agent,
                stage,
                message,
                status,
                details
            ));
    }

    private string GetVisualizationTitle(UnifiedVisualizationResponse visualization)
    {
        return visualization.ResponseType switch
        {
            ResponseType.Chart => visualization.ChartConfig?.ApexChartOptions?.Title?.Text ?? "Data Visualization",
            ResponseType.Table => "Data Table",
            ResponseType.Text => "Query Result",
            ResponseType.Mixed => "Analysis Results",
            _ => "Results"
        };
    }

    private string GenerateSummaryMessage(UnifiedVisualizationResponse visualization, int rowCount)
    {
        return visualization.ResponseType switch
        {
            ResponseType.Chart => $"Generated a {visualization.ChartConfig?.ChartType} chart with {rowCount} data points.",
            ResponseType.Table => $"Displaying {rowCount} rows in a table format.",
            ResponseType.Text => visualization.TextConfig?.IsSingleValue == true ? "Retrieved the requested value." : "Generated a summary of the results.",
            ResponseType.Mixed => "Generated a comprehensive analysis with visualizations.",
            _ => $"Successfully analyzed your query and generated results."
        };
    }

    private string[] ExtractColumnsFromQuery(string sqlQuery)
    {
        // Simple extraction - in production would use SQL parser
        var selectIndex = sqlQuery.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
        var fromIndex = sqlQuery.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);

        if (selectIndex >= 0 && fromIndex > selectIndex)
        {
            var selectClause = sqlQuery.Substring(selectIndex + 6, fromIndex - selectIndex - 6);
            return selectClause
                .Split(',')
                .Select(col => col.Trim())
                .Where(col => !string.IsNullOrEmpty(col))
                .ToArray();
        }

        return Array.Empty<string>();
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // Removed - data population is now handled by EnhancedVisualizationService













}