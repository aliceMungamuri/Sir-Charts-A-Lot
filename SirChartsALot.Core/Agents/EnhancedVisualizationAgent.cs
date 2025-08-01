using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SirChartsALot.Core.Configuration;
using SirChartsALot.Core.Models;
using System.Text.Json;

namespace SirChartsALot.Core.Agents;

public interface IEnhancedVisualizationAgent
{
    Task<VisualizationRecommendation> AnalyzeAndRecommendAsync(
        string userQuery,
        string sqlQuery,
        List<string> columns,
        List<Dictionary<string, object>> data,
        int totalRowCount);
}

public class EnhancedVisualizationAgent : IEnhancedVisualizationAgent
{
    private readonly Kernel _kernel;
    private readonly ILogger<EnhancedVisualizationAgent> _logger;
    private readonly AgentOptions _agentConfig;

    public EnhancedVisualizationAgent(Kernel kernel, ILogger<EnhancedVisualizationAgent> logger, AgentOptions agentConfig)
    {
        _kernel = kernel;
        _logger = logger;
        _agentConfig = agentConfig;
    }

    public async Task<VisualizationRecommendation> AnalyzeAndRecommendAsync(
        string userQuery,
        string sqlQuery,
        List<string> columns,
        List<Dictionary<string, object>> data,
        int totalRowCount)
    {
        _logger.LogInformation("Analyzing query results for visualization recommendation");

        // Take a sample of data for analysis (max 20 rows for AI context)
        var sampleData = data.Take(20).ToList();
        var sampleDataJson = JsonSerializer.Serialize(sampleData, new JsonSerializerOptions { WriteIndented = true });

        var prompt = $$"""
            You are a data visualization expert. Analyze this query result and recommend the best way to visualize it.
            
            User Query: {{userQuery}}
            SQL Query: {{sqlQuery}}
            Total Rows: {{totalRowCount}}
            Columns: {{JsonSerializer.Serialize(columns)}}
            
            Sample Data (first 20 rows):
            {{sampleDataJson}}
            
            ## Decision Criteria:
            
            ### Choose TEXT when:
            - Single value result (1 row, 1 column)
            - Query asks for count, sum, average, percentage
            - Example: "21.5%" or "$45,231.50"
            
            ### Choose TABLE when:
            - User explicitly asks for a "table", "list", or "details"
            - Multiple rows with many columns (>4 columns)
            - User wants to see detailed records or raw data
            - Data is text-heavy or contains IDs
            - Query asks to "show all" or "list all"
            
            IMPORTANT: If user says "table" or "as a table", ALWAYS return ResponseType.Table!
            
            ### Choose CHART when data shows:
            - Comparisons between categories:
              * Use Column (vertical bars) when ≤10 categories
              * Use Bar (horizontal bars) when >10 categories or long category names
              * Use Line when categories are ordered (like age ranges, income ranges)
            - Trends over time (dates/months/years) → Line or Area
            - Parts of a whole, percentages, shares → Pie or Donut (only if ≤10 items)
            - Correlations between two numeric values → Scatter
            - Distribution patterns → Histogram/BoxPlot
            
            IMPORTANT: Consider readability! If x-axis labels would overlap, use Bar (horizontal) instead of Column.
            
            ## Your Response:
            Based on the actual data, provide a simple recommendation following the VisualizationRecommendation schema.
            
            Key guidelines:
            - Look at the ACTUAL DATA to make your decision
            - For charts, specify which column index is for categories and which for values
            - For text, specify the format (Number, Currency, Percentage)
            - Provide a clear title and reasoning
            - Include a data insight that explains what the data shows
            
            Example response for a column chart:
            {
                "responseType": "Chart",
                "chartType": "Column", 
                "title": "Users by Filing Status",
                "reasoning": "Comparing counts across 6 filing status categories",
                "categoryColumnIndex": 0,
                "valueColumnIndices": [1],
                "dataInsight": "Married Filing Jointly has the highest count with 342 users"
            }
            """;

        var settings = new OpenAIPromptExecutionSettings 
        { 
            ResponseFormat = typeof(VisualizationRecommendation),
            Temperature = (_agentConfig.Model.StartsWith("o1") || _agentConfig.Model.StartsWith("o3") || _agentConfig.Model.StartsWith("o4")) 
                ? 1.0 : 0.3  // Lower temperature for more consistent recommendations
        };
        
        if (!_agentConfig.Model.StartsWith("o3") && !_agentConfig.Model.StartsWith("o4"))
        {
            settings.MaxTokens = 1000; // We need less tokens for simple recommendations
        }

        var args = new KernelArguments(settings);

        try
        {
            var response = await _kernel.InvokePromptAsync<string>(prompt, args);
            _logger.LogInformation("AI Visualization recommendation raw response: {Response}", response);
            
            var recommendation = JsonSerializer.Deserialize<VisualizationRecommendation>(response ?? "{}");
            
            if (recommendation == null)
            {
                _logger.LogWarning("Failed to deserialize recommendation, using fallback");
                throw new InvalidOperationException("Failed to deserialize recommendation");
            }

            _logger.LogInformation("Parsed recommendation: Type={Type}, ChartType={ChartType}, Reasoning={Reasoning}", 
                recommendation.ResponseType, recommendation.ChartType, recommendation.Reasoning);
            
            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting visualization recommendation for query: {Query}", userQuery);
            
            // Return a simple default based on data shape
            return CreateDefaultRecommendation(columns, data, totalRowCount);
        }
    }

    private VisualizationRecommendation CreateDefaultRecommendation(List<string> columns, List<Dictionary<string, object>> data, int totalRowCount)
    {
        _logger.LogWarning("Using fallback recommendation logic for {Columns} columns and {Rows} rows", columns.Count, totalRowCount);
        
        // Note: We can't access userQuery here in the fallback, but the AI should have caught explicit table requests
        
        // Simple heuristics for default recommendation
        if (totalRowCount == 1 && columns.Count <= 2)
        {
            return new VisualizationRecommendation
            {
                ResponseType = ResponseType.Text,
                Title = "Query Result",
                Reasoning = "Single value result (fallback recommendation)",
                TextFormat = TextFormatType.Number
            };
        }
        else if (columns.Count > 5 || totalRowCount > 50)
        {
            return new VisualizationRecommendation
            {
                ResponseType = ResponseType.Table,
                Title = "Query Results",
                Reasoning = "Multiple columns or many rows are best shown in a table (fallback recommendation)"
            };
        }
        else
        {
            // Try to be smarter about chart type based on data characteristics
            var chartType = ChartType.Column;
            var reasoning = "Default column chart for structured data (fallback recommendation)";
            
            // Check if we have a date/time column for line charts
            if (columns.Any(col => col.ToLower().Contains("date") || col.ToLower().Contains("time") || col.ToLower().Contains("month") || col.ToLower().Contains("year")))
            {
                chartType = ChartType.Line;
                reasoning = "Time-based data detected, using line chart (fallback recommendation)";
            }
            // Check if we have ordered ranges (like AGI, age ranges)
            else if (columns.Any(col => col.ToLower().Contains("range") || col.ToLower().Contains("agi") || col.ToLower().Contains("income") || col.ToLower().Contains("age")))
            {
                // For ordered numeric ranges, line chart is often better
                if (totalRowCount > 10)
                {
                    chartType = ChartType.Line;
                    reasoning = "Ordered range data with many categories, using line chart for better readability (fallback recommendation)";
                }
                else if (totalRowCount > 15)
                {
                    chartType = ChartType.Bar; // Horizontal bars
                    reasoning = "Many categories detected, using horizontal bar chart for readability (fallback recommendation)";
                }
            }
            // Check if data looks like parts of a whole
            else if (totalRowCount <= 10 && columns.Count == 2)
            {
                // Check if second column looks like it could be percentages or proportions
                var firstRow = data.FirstOrDefault();
                if (firstRow != null && columns.Count >= 2)
                {
                    var secondColName = columns[1].ToLower();
                    if (secondColName.Contains("percent") || secondColName.Contains("share") || secondColName.Contains("proportion"))
                    {
                        chartType = ChartType.Pie;
                        reasoning = "Data appears to show proportions or percentages (fallback recommendation)";
                    }
                }
            }
            // Many categories = horizontal bar chart
            else if (totalRowCount > 15)
            {
                chartType = ChartType.Bar;
                reasoning = "Many categories detected (>15), using horizontal bar chart for better label visibility (fallback recommendation)";
            }
            
            return new VisualizationRecommendation
            {
                ResponseType = ResponseType.Chart,
                ChartType = chartType,
                Title = "Data Visualization",
                Reasoning = reasoning,
                CategoryColumnIndex = 0,
                ValueColumnIndices = new List<int> { columns.Count > 1 ? 1 : 0 }
            };
        }
    }

    private void EnsureChartConfiguration(UnifiedVisualizationResponse viz, List<string> columns, List<object> data)
    {
        if (viz.ChartConfig?.ApexChartOptions == null)
        {
            _logger.LogWarning("Chart configuration missing, creating default");
            viz.ChartConfig = new ChartVisualization
            {
                ChartType = ChartType.Column,
                ApexChartOptions = CreateDefaultChartOptions(columns, data)
            };
        }
    }

    private void EnsureTableConfiguration(UnifiedVisualizationResponse viz, List<string> columns, List<object> data)
    {
        if (viz.TableConfig?.Columns == null || !viz.TableConfig.Columns.Any())
        {
            _logger.LogWarning("Table configuration missing, creating default");
            viz.TableConfig = new TableVisualization
            {
                Columns = columns.Select(col => new TableColumn
                {
                    Key = col,
                    DisplayName = FormatColumnName(col),
                    DataType = InferColumnDataType(col, data),
                    Sortable = true
                }).ToList(),
                EnablePagination = data.Count > 10,
                EnableSorting = true,
                EnableFiltering = data.Count > 20
            };
        }
    }

    private void EnsureTextConfiguration(UnifiedVisualizationResponse viz, List<object> data)
    {
        if (viz.TextConfig == null)
        {
            _logger.LogWarning("Text configuration missing, creating default");
            viz.TextConfig = new TextVisualization
            {
                Content = "No data available",
                FormatType = TextFormatType.Plain,
                IsSingleValue = false
            };
        }
    }

    private UnifiedVisualizationResponse CreateDefaultVisualization(List<string> columns, List<object> data, int totalRowCount)
    {
        // Simple heuristic for fallback
        if (columns.Count == 1 && data.Count == 1)
        {
            // Single value response
            return new UnifiedVisualizationResponse
            {
                ResponseType = ResponseType.Text,
                Confidence = 0.5,
                SelectionReasoning = "Single value result detected",
                TextConfig = new TextVisualization
                {
                    Content = data.First()?.ToString() ?? "No data",
                    FormatType = TextFormatType.Plain,
                    IsSingleValue = true
                }
            };
        }
        else if (columns.Count > 5 || totalRowCount > 50)
        {
            // Table for complex data
            return new UnifiedVisualizationResponse
            {
                ResponseType = ResponseType.Table,
                Confidence = 0.6,
                SelectionReasoning = "Multiple columns or many rows suggest tabular display",
                TableConfig = new TableVisualization
                {
                    Columns = columns.Select(col => new TableColumn
                    {
                        Key = col,
                        DisplayName = FormatColumnName(col),
                        DataType = InferColumnDataType(col, data),
                        Sortable = true
                    }).ToList(),
                    EnablePagination = totalRowCount > 10,
                    EnableSorting = true
                }
            };
        }
        else
        {
            // Default to column chart
            return new UnifiedVisualizationResponse
            {
                ResponseType = ResponseType.Chart,
                Confidence = 0.5,
                SelectionReasoning = "Default visualization for structured data",
                ChartConfig = new ChartVisualization
                {
                    ChartType = ChartType.Column,
                    ApexChartOptions = CreateDefaultChartOptions(columns, data)
                }
            };
        }
    }

    private ApexOptions CreateDefaultChartOptions(List<string> columns, List<object> data)
    {
        // Create a basic column chart configuration
        var series = new List<object>();
        var labels = new List<string>();

        if (data.Any() && data.First() is Dictionary<string, object>)
        {
            foreach (var row in data.Cast<Dictionary<string, object>>())
            {
                if (columns.Count >= 2)
                {
                    labels.Add(row[columns[0]]?.ToString() ?? "");
                    series.Add(row[columns[1]]);
                }
            }
        }

        return new ApexOptions
        {
            Chart = new Chart
            {
                Type = "bar",
                Height = 350
            },
            Series = JsonSerializer.Serialize(series),
            Labels = labels,
            Title = new Title
            {
                Text = "Data Visualization",
                Align = "center"
            },
            DataLabels = new DataLabels
            {
                Enabled = false
            },
            Grid = new Grid
            {
                BorderColor = "#e7e7e7",
                StrokeDashArray = 5
            },
            Legend = new Legend
            {
                Position = "bottom"
            }
        };
    }

    private string FormatColumnName(string columnName)
    {
        // Convert snake_case or camelCase to Title Case
        return System.Text.RegularExpressions.Regex.Replace(
            columnName,
            "(\\B[A-Z]|_[a-z])",
            " $1"
        ).Trim().Replace("_", "");
    }

    private ColumnDataType InferColumnDataType(string columnName, List<object> data)
    {
        var lowerName = columnName.ToLower();
        
        if (lowerName.Contains("date") || lowerName.Contains("time"))
            return ColumnDataType.DateTime;
        if (lowerName.Contains("price") || lowerName.Contains("cost") || lowerName.Contains("amount") || lowerName.Contains("salary"))
            return ColumnDataType.Currency;
        if (lowerName.Contains("percent") || lowerName.Contains("rate"))
            return ColumnDataType.Percentage;
        if (lowerName.Contains("url") || lowerName.Contains("link"))
            return ColumnDataType.Link;
        
        // Check first non-null value
        var firstValue = data.FirstOrDefault(d => d is Dictionary<string, object> dict && dict.ContainsKey(columnName) && dict[columnName] != null);
        if (firstValue is Dictionary<string, object> valueDict && valueDict.ContainsKey(columnName))
        {
            var value = valueDict[columnName];
            if (value is int || value is long || value is float || value is double || value is decimal)
                return ColumnDataType.Number;
            if (value is bool)
                return ColumnDataType.Boolean;
            if (value is DateTime)
                return ColumnDataType.DateTime;
        }
        
        return ColumnDataType.String;
    }

    private object AnalyzeDataCharacteristics(List<string> columns, List<object> sampleData)
    {
        var dataTypes = new Dictionary<string, string>();
        var uniqueCounts = new Dictionary<string, int>();
        
        var characteristics = new Dictionary<string, object>
        {
            ["ColumnCount"] = columns.Count,
            ["HasNumericColumns"] = false,
            ["HasDateColumns"] = false,
            ["HasTextColumns"] = false,
            ["DataTypes"] = dataTypes,
            ["UniqueValueCounts"] = uniqueCounts
        };

        if (sampleData.Count == 0) return characteristics;

        // Analyze data types from sample
        for (int i = 0; i < columns.Count; i++)
        {
            var columnName = columns[i];
            var values = new List<object>();
            
            // Get values for this column
            foreach (var row in sampleData.Take(10))
            {
                if (row is IDictionary<string, object> dict && dict.ContainsKey(columnName))
                {
                    values.Add(dict[columnName]);
                }
            }

            // Determine data type
            var dataType = "Unknown";
            var uniqueValues = values.Where(v => v != null).Distinct().Count();
            
            if (values.Any(v => v != null))
            {
                var firstNonNull = values.First(v => v != null);
                if (firstNonNull is int or long or decimal or double or float)
                {
                    dataType = "Numeric";
                    characteristics["HasNumericColumns"] = true;
                }
                else if (firstNonNull is DateTime)
                {
                    dataType = "DateTime";
                    characteristics["HasDateColumns"] = true;
                }
                else
                {
                    dataType = "Text";
                    characteristics["HasTextColumns"] = true;
                }
            }

            dataTypes[columnName] = dataType;
            uniqueCounts[columnName] = uniqueValues;
        }

        return characteristics;
    }
}