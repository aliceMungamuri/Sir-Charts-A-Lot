using Microsoft.AspNetCore.SignalR;
using SirChartsALot.Api.Models;
using SirChartsALot.Api.Services;
using SirChartsALot.Core.Agents;
using SirChartsALot.Core.Services;
using System.Text;
using System.Text.Json;

namespace SirChartsALot.Api.Hubs
{
    public class ChartHub : Hub
    {
        private readonly IDomainExpertAgent _domainExpert;
        private readonly ISqlQueryExpertAgent _sqlExpert;
        private readonly ISqlExecutionService _sqlExecutor;
        private readonly ISemanticKernelService _semanticKernelService;
        private readonly ISchemaIntrospectionService _schemaIntrospectionService;
        private readonly ILogger<ChartHub> _logger;
        
        public ChartHub(
            IDomainExpertAgent domainExpert,
            ISqlQueryExpertAgent sqlExpert,
            ISqlExecutionService sqlExecutor,
            ISemanticKernelService semanticKernelService,
            ISchemaIntrospectionService schemaIntrospectionService,
            ILogger<ChartHub> logger)
        {
            _domainExpert = domainExpert;
            _sqlExpert = sqlExpert;
            _sqlExecutor = sqlExecutor;
            _semanticKernelService = semanticKernelService;
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
                    throw new InvalidOperationException(
                        "No database schema found. Please ensure a valid database connection is configured in appsettings.json");
                }
                
                _logger.LogInformation("Schema cache contains {TableCount} tables", schemaCache.Tables.Count);
                
                // Step 1: Domain Expert Analysis
                await Clients.Caller.SendAsync("TimelineEvent", 
                    new TimelineEventMessage(
                        Guid.NewGuid().ToString(),
                        "domain-expert",
                        "analyzing",
                        "Understanding your natural language query...",
                        "active"
                    ));
                
                var domainAnalysis = await _domainExpert.AnalyzeQueryAsync(query);
                
                await Clients.Caller.SendAsync("TimelineEvent", 
                    new TimelineEventMessage(
                        Guid.NewGuid().ToString(),
                        "domain-expert",
                        "identified",
                        $"Identified relevant tables: {string.Join(", ", domainAnalysis.Tables)}",
                        "completed",
                        new[] { 
                            $"Intent: {domainAnalysis.Intent}",
                            $"Complexity: {domainAnalysis.Complexity}",
                            $"Relationships: {domainAnalysis.Relationships}"
                        }
                    ));
                
                // Step 2: SQL Generation
                await Clients.Caller.SendAsync("TimelineEvent", 
                    new TimelineEventMessage(
                        Guid.NewGuid().ToString(),
                        "sql-expert",
                        "generating",
                        "Generating precise SQL query...",
                        "active"
                    ));
                
                string sqlQuery;
                try
                {
                    sqlQuery = await _sqlExpert.GenerateSqlAsync(domainAnalysis, query);
                    
                    await Clients.Caller.SendAsync("TimelineEvent", 
                        new TimelineEventMessage(
                            Guid.NewGuid().ToString(),
                            "sql-expert",
                            "generated",
                            "SQL query generated successfully",
                            "completed"
                        ));
                    
                    // Send the generated SQL to the frontend
                    await Clients.Caller.SendAsync("SqlGenerated", 
                        new SqlGeneratedMessage(
                            sqlQuery,
                            domainAnalysis.Tables.ToArray(),
                            Array.Empty<string>(),
                            true
                        ));
                }
                catch (Exception sqlEx)
                {
                    _logger.LogError(sqlEx, "Error generating SQL");
                    
                    await Clients.Caller.SendAsync("TimelineEvent", 
                        new TimelineEventMessage(
                            Guid.NewGuid().ToString(),
                            "sql-expert",
                            "error",
                            "Failed to generate SQL query",
                            "error",
                            new[] { sqlEx.Message }
                        ));
                    
                    throw;
                }
                
                // Step 3: Execute Query
                await Clients.Caller.SendAsync("TimelineEvent", 
                    new TimelineEventMessage(
                        Guid.NewGuid().ToString(),
                        "executor",
                        "executing",
                        "Running query against database...",
                        "active"
                    ));
                
                var columns = new List<string>();
                var allData = new List<object>();
                var rowCount = 0;
                
                await foreach (var batch in _sqlExecutor.StreamResultsAsync(sqlQuery))
                {
                    if (columns.Count == 0)
                    {
                        columns = batch.Columns;
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
                    rowCount += batch.Rows.Count;
                    
                    // Stream data to client
                    await Clients.Caller.SendAsync("DataStream", 
                        new DataStreamMessage(
                            dataObjects.ToArray(),
                            columns.ToArray(),
                            batch.IsComplete
                        ));
                }
                
                await Clients.Caller.SendAsync("TimelineEvent", 
                    new TimelineEventMessage(
                        Guid.NewGuid().ToString(),
                        "executor",
                        "completed",
                        $"Query executed successfully. Retrieved {rowCount} rows",
                        "completed"
                    ));
                
                // Step 4: Generate Visualization
                await Clients.Caller.SendAsync("TimelineEvent", 
                    new TimelineEventMessage(
                        Guid.NewGuid().ToString(),
                        "viz-agent",
                        "analyzing",
                        "Analyzing data for visualization...",
                        "active"
                    ));
                
                // For now, use simple visualization logic
                var vizType = DetermineVisualizationType(columns, allData);
                var vizTitle = GenerateVisualizationTitle(domainAnalysis.Intent);
                
                await Clients.Caller.SendAsync("TimelineEvent", 
                    new TimelineEventMessage(
                        Guid.NewGuid().ToString(),
                        "viz-agent",
                        "completed",
                        $"Selected {vizType} chart for visualization",
                        "completed"
                    ));
                
                // Complete thinking
                var duration = (int)(DateTime.Now - startTime).TotalSeconds;
                
                var vizData = await _semanticKernelService.RunMiniVizAgent(columns, allData);
                await Clients.Caller.SendAsync("ThinkingComplete", new ThinkingCompleteMessage(duration));
                // Send visualization ready
                var vizTag = JsonSerializer.Serialize(vizData.ApexChartOptions);
                _logger.LogInformation("Generated visualization tag: {VizTag}", vizTag);
                await Clients.Caller.SendAsync("VisualizationReady", 
                    new VisualizationMessage(
                        vizData.ApexChartOptions.Chart.Type,
                        vizData.ApexChartOptions.Title.Text,
                       vizTag
                        //GenerateVizTag(vizType, vizTitle, columns, allData)
                    ));
                
                // Send a summary message
                var summaryMessage = $"Successfully analyzed your query and generated a {vizType} chart.";
                await Clients.Caller.SendAsync("ReceiveMessage", summaryMessage);
                
                _logger.LogInformation("Successfully processed query end-to-end");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing query: {Query}", query);
                
                // Send error timeline event
                await Clients.Caller.SendAsync("TimelineEvent", 
                    new TimelineEventMessage(
                        Guid.NewGuid().ToString(),
                        "system",
                        "error",
                        "Error processing query",
                        "error",
                        new[] { ex.Message }
                    ));
                
                await Clients.Caller.SendAsync("ThinkingComplete", new ThinkingCompleteMessage(0));
                
                await Clients.Caller.SendAsync("ReceiveMessage", 
                    $"I apologize, but I encountered an error processing your request: {ex.Message}");
            }
        }
        
        private string DetermineVisualizationType(List<string> columns, List<object> data)
        {
            // Simple heuristics for chart type selection
            if (columns.Count < 2)
                return "column";
                
            var firstCol = columns[0].ToLower();
            var secondCol = columns[1].ToLower();
            
            // Check for time-based data
            if (firstCol.Contains("date") || firstCol.Contains("month") || firstCol.Contains("year") || 
                firstCol.Contains("time") || firstCol.Contains("quarter") || firstCol.Contains("week"))
            {
                return "line";
            }
            
            // Check for percentage/proportion data suitable for pie charts
            if ((secondCol.Contains("percentage") || secondCol.Contains("percent") || 
                 secondCol.Contains("share") || secondCol.Contains("proportion")) && 
                data.Count <= 10)
            {
                return "pie";
            }
            
            // Check if it's categorical data with counts/amounts
            if ((secondCol.Contains("count") || secondCol.Contains("total") || 
                 secondCol.Contains("sum") || secondCol.Contains("amount") ||
                 secondCol.Contains("quantity") || secondCol.Contains("number")) &&
                data.Count <= 20)
            {
                // Use pie chart for small datasets showing composition
                if (data.Count <= 6 && (firstCol.Contains("status") || firstCol.Contains("type") || 
                    firstCol.Contains("category") || firstCol.Contains("group")))
                {
                    return "pie";
                }
                return "column";
            }
            
            // For correlation data
            if (data.Count > 20 && columns.All(c => 
                c.ToLower().Contains("value") || c.ToLower().Contains("score") || 
                c.ToLower().Contains("amount") || c.ToLower().Contains("price")))
            {
                return "scatter";
            }
            
            // Default to column chart
            return "column";
        }
        
        private string GenerateVisualizationTitle(string intent)
        {
            // Clean up the intent for a title
            return intent.Length > 50 ? intent.Substring(0, 47) + "..." : intent;
        }
        
        private string GenerateVizTag(string vizType, string title, List<string> columns, List<object> data)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[VIZ:{vizType}:{title}]");
            
            // Format data based on visualization type
            if (data.Count > 0 && columns.Count >= 2)
            {
                var labelColumn = columns[0];
                var valueColumn = columns[1];
                
                if (vizType == "pie")
                {
                    // For pie charts: label:value pairs
                    for (int i = 0; i < data.Count; i++)
                    {
                        if (data[i] is Dictionary<string, object> dict)
                        {
                            var label = dict.ContainsKey(labelColumn) ? dict[labelColumn]?.ToString() ?? "" : "";
                            // Replace empty labels with "Not Specified"
                            if (string.IsNullOrWhiteSpace(label))
                                label = "Not Specified";
                            var value = dict.ContainsKey(valueColumn) ? dict[valueColumn]?.ToString() ?? "0" : "0";
                            sb.Append($"{label}:{value}");
                            if (i < data.Count - 1)
                                sb.Append(",");
                        }
                    }
                }
                else if (vizType == "line" || vizType == "column" || vizType == "area")
                {
                    // For line/column/area charts: label:value pairs
                    for (int i = 0; i < data.Count; i++)
                    {
                        if (data[i] is Dictionary<string, object> dict)
                        {
                            var label = dict.ContainsKey(labelColumn) ? dict[labelColumn]?.ToString() ?? "" : "";
                            // Replace empty labels with "Not Specified"
                            if (string.IsNullOrWhiteSpace(label))
                                label = "Not Specified";
                            var value = dict.ContainsKey(valueColumn) ? dict[valueColumn]?.ToString() ?? "0" : "0";
                            sb.Append($"{label}:{value}");
                            if (i < data.Count - 1)
                                sb.Append(",");
                        }
                    }
                }
                else if (vizType == "scatter")
                {
                    // For scatter plots: x|y pairs
                    for (int i = 0; i < data.Count; i++)
                    {
                        if (data[i] is Dictionary<string, object> dict)
                        {
                            var x = dict.ContainsKey(labelColumn) ? dict[labelColumn]?.ToString() ?? "0" : "0";
                            var y = dict.ContainsKey(valueColumn) ? dict[valueColumn]?.ToString() ?? "0" : "0";
                            sb.Append($"{x}|{y}");
                            if (i < data.Count - 1)
                                sb.Append(",");
                        }
                    }
                }
            }
            
            sb.AppendLine();
            sb.Append("[/VIZ]");
            
            return sb.ToString();
        }
    }
}