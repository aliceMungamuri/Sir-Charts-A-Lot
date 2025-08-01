using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using SirChartsALot.Core.Agents;
using SirChartsALot.Core.Configuration;
using SirChartsALot.Core.Models;
using SirChartsALot.Core.Models.HttpHandlers;
using System.Text.Json;

namespace SirChartsALot.Api.Services;

public interface IEnhancedVisualizationService
{
    Task<UnifiedVisualizationResponse> GenerateVisualizationAsync(
        string userQuery,
        string sqlQuery,
        List<string> columns,
        List<object> data);

    Task<VisualizationRecommendation> GetRecommendationAsync(
        string userQuery,
        string sqlQuery,
        List<string> columns,
        List<object> data);
        
    UnifiedVisualizationResponse BuildVisualizationFromRecommendation(
        VisualizationRecommendation recommendation,
        List<string> columns,
        List<object> data);
}

public class EnhancedVisualizationService : IEnhancedVisualizationService
{
    private readonly IEnhancedVisualizationAgent _visualizationAgent;
    private readonly ILogger<EnhancedVisualizationService> _logger;
    private readonly AzureOpenAIOptions _azureOptions;
    private readonly AgentOptions _agentConfig;

    public EnhancedVisualizationService(
        IOptions<AzureOpenAIOptions> azureOptions,
        IOptions<AgentsConfiguration> agentOptions,
        ILogger<EnhancedVisualizationService> logger,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _azureOptions = azureOptions.Value;
        _agentConfig = agentOptions.Value.Visualization;
        
        // Create kernel for visualization agent using configured model
        var httpClient = DelegateHandlerFactory.GetHttpClient<SystemToDeveloperRoleHandler>(loggerFactory);
        var kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                deploymentName: _agentConfig.Model, // Use configured model from appsettings
                endpoint: _azureOptions.Endpoint,
                apiKey: _azureOptions.ApiKey, httpClient:httpClient)
            .Build();
            
        _visualizationAgent = new EnhancedVisualizationAgent(kernel, 
            loggerFactory.CreateLogger<EnhancedVisualizationAgent>(),
            _agentConfig);
    }

    public async Task<UnifiedVisualizationResponse> GenerateVisualizationAsync(
        string userQuery,
        string sqlQuery,
        List<string> columns,
        List<object> data)
    {
        // New simplified approach: Get recommendation, then build visualization
        var recommendation = await GetRecommendationAsync(userQuery, sqlQuery, columns, data);
        return BuildVisualizationFromRecommendation(recommendation, columns, data);
    }

    public async Task<VisualizationRecommendation> GetRecommendationAsync(
        string userQuery,
        string sqlQuery,
        List<string> columns,
        List<object> data)
    {
        try
        {
            _logger.LogInformation("Getting visualization recommendation for query: {Query}", userQuery);
            var startTime = DateTime.UtcNow;
            
            // Convert data to the format the agent expects
            var dataDict = data.Select(row => row as Dictionary<string, object> ?? new Dictionary<string, object>()).ToList();
            
            // Call the refactored visualization agent
            var recommendation = await _visualizationAgent.AnalyzeAndRecommendAsync(
                userQuery,
                sqlQuery,
                columns,
                dataDict,
                data.Count
            );
            
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation("Got recommendation: {ResponseType} ({ChartType}) in {Duration:F2} seconds",
                recommendation.ResponseType, recommendation.ChartType, duration);
            
            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting visualization recommendation");
            return CreateFallbackRecommendation(columns, data);
        }
    }

    public UnifiedVisualizationResponse BuildVisualizationFromRecommendation(
        VisualizationRecommendation recommendation,
        List<string> columns,
        List<object> data)
    {
        _logger.LogInformation("Building {ResponseType} visualization from recommendation", recommendation.ResponseType);
        
        // Post-process recommendation based on actual data size
        if (recommendation.ResponseType == ResponseType.Chart)
        {
            // Count unique categories
            var categoryIndex = recommendation.CategoryColumnIndex ?? 0;
            var uniqueCategories = new HashSet<string>();
            
            foreach (var row in data)
            {
                if (row is Dictionary<string, object> dict && categoryIndex < columns.Count)
                {
                    uniqueCategories.Add(dict[columns[categoryIndex]]?.ToString() ?? "Unknown");
                }
            }
            
            _logger.LogInformation("Post-processing chart type: Current={Type}, UniqueCategories={Count}, Columns={Columns}", 
                recommendation.ChartType, uniqueCategories.Count, string.Join(",", columns));
            
            // Adjust chart type based on category count
            if (recommendation.ChartType == ChartType.Column || recommendation.ChartType == ChartType.Bar)
            {
                if (uniqueCategories.Count > 15)
                {
                    var oldType = recommendation.ChartType;
                    recommendation.ChartType = ChartType.Bar; // Horizontal bars
                    recommendation.Reasoning += $" (Adjusted from {oldType} to horizontal bar chart due to {uniqueCategories.Count} categories)";
                    _logger.LogInformation("Converted {OldType} to Bar chart due to {Count} categories", oldType, uniqueCategories.Count);
                }
                else if (uniqueCategories.Count > 8)
                {
                    // Check if data appears to be sequential/ordered
                    var sortedCategories = uniqueCategories.OrderBy(c => 
                    {
                        if (double.TryParse(c.Replace(",", "").Replace("$", "").Replace("-", ""), out double val))
                            return val;
                        return double.MaxValue;
                    }).ToList();
                    
                    // If categories are numeric and sequential, consider line chart
                    var isNumericSequence = sortedCategories.Take(5).All(c => 
                        double.TryParse(c.Replace(",", "").Replace("$", "").Replace("-", ""), out _));
                    
                    if (isNumericSequence)
                    {
                        var oldType = recommendation.ChartType;
                        recommendation.ChartType = ChartType.Line;
                        recommendation.Reasoning += $" (Adjusted from {oldType} to line chart for sequential numeric data with {uniqueCategories.Count} points)";
                        _logger.LogInformation("Converted {OldType} to Line chart for sequential data with {Count} categories", oldType, uniqueCategories.Count);
                    }
                }
            }
        }
        
        switch (recommendation.ResponseType)
        {
            case ResponseType.Chart:
                return BuildChartVisualization(recommendation, columns, data);
            case ResponseType.Table:
                return BuildTableVisualization(recommendation, columns, data);
            case ResponseType.Text:
                return BuildTextVisualization(recommendation, columns, data);
            case ResponseType.Mixed:
                return BuildMixedVisualization(recommendation, columns, data);
            default:
                throw new InvalidOperationException($"Unknown response type: {recommendation.ResponseType}");
        }
    }

    private void ValidateChartResponse(UnifiedVisualizationResponse response)
    {
        if (response.ChartConfig?.ApexChartOptions == null)
        {
            throw new InvalidOperationException("Chart response missing ApexChartOptions");
        }
        
        // Ensure chart type matches
        var chartTypeString = response.ChartConfig.ChartType.ToString().ToLower();
        if (response.ChartConfig.ApexChartOptions.Chart.Type != chartTypeString)
        {
            _logger.LogWarning("Chart type mismatch, correcting from {Old} to {New}", 
                response.ChartConfig.ApexChartOptions.Chart.Type, chartTypeString);
            response.ChartConfig.ApexChartOptions.Chart.Type = chartTypeString;
        }
    }

    private void ValidateTableResponse(UnifiedVisualizationResponse response)
    {
        if (response.TableConfig?.Columns == null || !response.TableConfig.Columns.Any())
        {
            throw new InvalidOperationException("Table response missing column definitions");
        }
    }

    private void ValidateTextResponse(UnifiedVisualizationResponse response)
    {
        if (response.TextConfig == null || string.IsNullOrEmpty(response.TextConfig.Content))
        {
            throw new InvalidOperationException("Text response missing content");
        }
    }

    private void ValidateMixedResponse(UnifiedVisualizationResponse response)
    {
        if (response.SecondaryVisualizations == null || !response.SecondaryVisualizations.Any())
        {
            throw new InvalidOperationException("Mixed response missing secondary visualizations");
        }
    }

    private UnifiedVisualizationResponse BuildChartVisualization(
        VisualizationRecommendation recommendation,
        List<string> columns,
        List<object> data)
    {
        if (recommendation.ChartType == null)
        {
            throw new InvalidOperationException("Chart type not specified in recommendation");
        }

        var chartConfig = new ChartVisualization
        {
            ChartType = recommendation.ChartType.Value,
            ApexChartOptions = BuildApexChartOptions(recommendation, columns, data)
        };

        return new UnifiedVisualizationResponse
        {
            ResponseType = ResponseType.Chart,
            Confidence = 0.9, // High confidence since AI analyzed actual data
            SelectionReasoning = recommendation.Reasoning,
            ChartConfig = chartConfig
        };
    }

    private UnifiedVisualizationResponse BuildTableVisualization(
        VisualizationRecommendation recommendation,
        List<string> columns,
        List<object> data)
    {
        var tableConfig = new TableVisualization
        {
            Columns = columns.Select((col, index) => new TableColumn
            {
                Key = col,
                DisplayName = FormatColumnName(col),
                DataType = InferColumnDataType(col, data),
                Sortable = true
            }).ToList(),
            EnablePagination = data.Count > 10,
            PageSize = 10,
            EnableSorting = true,
            EnableFiltering = data.Count > 20,
            EnableExport = true
        };

        return new UnifiedVisualizationResponse
        {
            ResponseType = ResponseType.Table,
            Confidence = 0.9,
            SelectionReasoning = recommendation.Reasoning,
            TableConfig = tableConfig
        };
    }

    private UnifiedVisualizationResponse BuildTextVisualization(
        VisualizationRecommendation recommendation,
        List<string> columns,
        List<object> data)
    {
        var textContent = "";
        var isSingleValue = false;
        SingleValueMetadata? metadata = null;

        if (data.Count == 1 && columns.Count >= 1)
        {
            var firstRow = data[0] as Dictionary<string, object>;
            if (firstRow != null)
            {
                var value = firstRow.Values.FirstOrDefault();
                textContent = FormatValue(value, recommendation.TextFormat ?? TextFormatType.Plain);
                isSingleValue = true;
                
                if (value != null && IsNumeric(value))
                {
                    metadata = new SingleValueMetadata
                    {
                        NumericValue = Convert.ToDouble(value),
                        Unit = InferUnit(columns[0])
                    };
                }
            }
        }

        return new UnifiedVisualizationResponse
        {
            ResponseType = ResponseType.Text,
            Confidence = 0.9,
            SelectionReasoning = recommendation.Reasoning,
            TextConfig = new TextVisualization
            {
                Content = textContent,
                FormatType = recommendation.TextFormat ?? TextFormatType.Plain,
                IsSingleValue = isSingleValue,
                SingleValueMetadata = metadata,
                Highlights = recommendation.DataInsight != null ? new List<string> { recommendation.DataInsight } : null
            }
        };
    }

    private UnifiedVisualizationResponse BuildMixedVisualization(
        VisualizationRecommendation recommendation,
        List<string> columns,
        List<object> data)
    {
        // For mixed responses, create a text summary with a supporting chart
        var textConfig = new TextVisualization
        {
            Content = recommendation.DataInsight ?? "Analysis complete",
            FormatType = TextFormatType.Summary,
            IsSingleValue = false,
            UseMarkdown = true
        };

        var secondaryVisualizations = new List<SecondaryVisualization>();
        
        if (recommendation.ChartType != null)
        {
            var chartViz = BuildChartVisualization(recommendation, columns, data);
            secondaryVisualizations.Add(new SecondaryVisualization
            {
                Order = 1,
                ResponseType = ResponseType.Chart,
                Configuration = JsonSerializer.Serialize(chartViz.ChartConfig)
            });
        }

        return new UnifiedVisualizationResponse
        {
            ResponseType = ResponseType.Mixed,
            Confidence = 0.9,
            SelectionReasoning = recommendation.Reasoning,
            TextConfig = textConfig,
            SecondaryVisualizations = secondaryVisualizations
        };
    }

    private ApexOptions BuildApexChartOptions(
        VisualizationRecommendation recommendation,
        List<string> columns,
        List<object> data)
    {
        var chartType = recommendation.ChartType!.Value;
        
        // Convert ChartType enum to ApexCharts type string
        var apexChartType = chartType switch
        {
            ChartType.Column => "bar",
            ChartType.Bar => "bar",
            ChartType.Line => "line",
            ChartType.Area => "area",
            ChartType.Pie => "pie",
            ChartType.Donut => "donut",
            ChartType.Scatter => "scatter",
            ChartType.Bubble => "bubble",
            ChartType.Heatmap => "heatmap",
            ChartType.Treemap => "treemap",
            ChartType.RadialBar => "radialBar",
            ChartType.Radar => "radar",
            ChartType.PolarArea => "polarArea",
            _ => "bar"
        };

        var options = new ApexOptions
        {
            Chart = new Chart
            {
                Type = apexChartType,
                Height = 350,
                Toolbar = new Toolbar { Show = true }
            },
            Title = new Title
            {
                Text = recommendation.Title,
                Align = "center",
                Style = new Style
                {
                    FontSize = "16px",
                    FontWeight = 600,
                    Color = "#263238"
                }
            },
            DataLabels = new DataLabels { Enabled = false },
            Grid = new Grid
            {
                BorderColor = "#e7e7e7",
                StrokeDashArray = 4
            },
            Legend = new Legend
            {
                Position = "bottom"
            },
            Series = "[]", // Will be populated by PopulateChartData
            Labels = new List<string>() // Will be populated for pie charts
        };

        // Populate data based on chart type and recommendation
        PopulateChartData(options, chartType, recommendation, columns, data);

        return options;
    }

    private void PopulateChartData(
        ApexOptions options,
        ChartType chartType,
        VisualizationRecommendation recommendation,
        List<string> columns,
        List<object> data)
    {
        switch (chartType)
        {
            case ChartType.Pie:
            case ChartType.Donut:
            case ChartType.RadialBar:
            case ChartType.PolarArea:
                PopulatePieChartData(options, recommendation, columns, data);
                break;
            
            case ChartType.Scatter:
            case ChartType.Bubble:
                PopulateScatterChartData(options, recommendation, columns, data);
                break;
            
            default:
                PopulateCategoryChartData(options, recommendation, columns, data);
                break;
        }

        // For bar charts, the horizontal property is set via the chart configuration
        // ApexCharts handles horizontal bars differently than vertical columns
    }

    private void PopulateCategoryChartData(
        ApexOptions options,
        VisualizationRecommendation recommendation,
        List<string> columns,
        List<object> data)
    {
        _logger.LogInformation("PopulateCategoryChartData: columns={Columns}, dataCount={Count}", 
            string.Join(",", columns), data.Count);
        
        var categories = new List<string>();
        var seriesList = new List<object>();

        // Validate column indices
        var categoryIndex = recommendation.CategoryColumnIndex ?? 0;
        var valueIndices = recommendation.ValueColumnIndices ?? new List<int> { 1 };
        
        // Ensure indices are valid
        if (categoryIndex >= columns.Count)
        {
            categoryIndex = 0;
        }
        
        valueIndices = valueIndices.Where(idx => idx < columns.Count).ToList();
        if (!valueIndices.Any() && columns.Count > 1)
        {
            valueIndices = new List<int> { 1 };
        }
        
        // Check if we need to aggregate the data (when we have duplicate categories)
        var needsAggregation = false;
        var categoryValues = new List<string>();
        
        foreach (var row in data)
        {
            if (row is Dictionary<string, object> dict && categoryIndex < columns.Count)
            {
                var category = dict[columns[categoryIndex]]?.ToString() ?? "Unknown";
                categoryValues.Add(category);
            }
        }
        
        // If we have duplicate categories, we need to aggregate
        needsAggregation = categoryValues.Count != categoryValues.Distinct().Count();
        _logger.LogInformation("Category chart aggregation needed: {Needed}, Total categories: {Total}, Unique: {Unique}", 
            needsAggregation, categoryValues.Count, categoryValues.Distinct().Count());
        
        if (needsAggregation)
        {
            // Aggregate data by category
            var aggregatedData = new Dictionary<string, Dictionary<int, double>>();
            
            foreach (var row in data)
            {
                if (row is Dictionary<string, object> dict && categoryIndex < columns.Count)
                {
                    var category = dict[columns[categoryIndex]]?.ToString() ?? "Unknown";
                    
                    if (!aggregatedData.ContainsKey(category))
                    {
                        aggregatedData[category] = new Dictionary<int, double>();
                    }
                    
                    // Sum values for each value column
                    foreach (var valueIndex in valueIndices)
                    {
                        if (valueIndex < columns.Count && dict.ContainsKey(columns[valueIndex]))
                        {
                            var value = dict[columns[valueIndex]];
                            if (value != null && double.TryParse(value.ToString(), out double numericValue))
                            {
                                if (!aggregatedData[category].ContainsKey(valueIndex))
                                {
                                    aggregatedData[category][valueIndex] = 0;
                                }
                                aggregatedData[category][valueIndex] += numericValue;
                            }
                        }
                    }
                }
            }
            
            // Sort categories (especially important for numeric ranges like AGI)
            var sortedCategories = aggregatedData.Keys.OrderBy(k => 
            {
                // Try to parse as number for proper sorting
                if (double.TryParse(k.Replace(",", "").Replace("$", ""), out double numValue))
                {
                    return numValue;
                }
                return double.MaxValue;
            }).ToList();
            
            categories = sortedCategories;
            
            // Build series from aggregated data
            foreach (var valueIndex in valueIndices)
            {
                var values = new List<object>();
                foreach (var category in sortedCategories)
                {
                    if (aggregatedData[category].ContainsKey(valueIndex))
                    {
                        values.Add(aggregatedData[category][valueIndex]);
                    }
                    else
                    {
                        values.Add(0);
                    }
                }
                
                seriesList.Add(new
                {
                    name = valueIndex < columns.Count ? columns[valueIndex] : "Value",
                    data = values
                });
            }
            
            _logger.LogInformation("Aggregated category data: {CatCount} categories", categories.Count);
        }
        else
        {
            // Use original logic for non-aggregated data
            foreach (var row in data)
            {
                if (row is Dictionary<string, object> dict && categoryIndex < columns.Count)
                {
                    categories.Add(dict[columns[categoryIndex]]?.ToString() ?? "Unknown");
                }
            }

            foreach (var valueIndex in valueIndices)
            {
                if (valueIndex < columns.Count)
                {
                    var values = new List<object>();
                    foreach (var row in data)
                    {
                        if (row is Dictionary<string, object> dict && dict.ContainsKey(columns[valueIndex]))
                        {
                            var value = dict[columns[valueIndex]];
                            // Ensure numeric values are properly typed
                            if (value != null && double.TryParse(value.ToString(), out double numericValue))
                            {
                                values.Add(numericValue);
                            }
                            else if (value != null)
                            {
                                values.Add(value);
                            }
                        }
                    }

                    seriesList.Add(new
                    {
                        name = columns[valueIndex],
                        data = values
                    });
                }
            }
        }

        options.Series = JsonSerializer.Serialize(seriesList);
        options.XAxis = new XAxis { Categories = categories };
    }

    private void PopulatePieChartData(
        ApexOptions options,
        VisualizationRecommendation recommendation,
        List<string> columns,
        List<object> data)
    {
        _logger.LogInformation("PopulatePieChartData: columns={Columns}, dataCount={Count}, categoryIndex={CatIdx}, valueIndex={ValIdx}", 
            string.Join(",", columns), data.Count, recommendation.CategoryColumnIndex, recommendation.ValueColumnIndices?.FirstOrDefault());
        
        var labels = new List<string>();
        var series = new List<object>();

        var categoryIndex = recommendation.CategoryColumnIndex ?? 0;
        var valueIndex = recommendation.ValueColumnIndices?.FirstOrDefault() ?? 1;

        // Check if we need to aggregate the data (e.g., when we have boolean values or repeated categories)
        if (data.Count > 1 && columns.Count >= 2)
        {
            // Check if this looks like raw data that needs aggregation
            var needsAggregation = false;
            var firstRow = data.FirstOrDefault() as Dictionary<string, object>;
            
            if (firstRow != null)
            {
                // Check if we have a boolean/categorical column that needs grouping
                foreach (var col in columns)
                {
                    if (firstRow.ContainsKey(col))
                    {
                        var value = firstRow[col]?.ToString()?.ToLower();
                        if (value == "true" || value == "false" || value == "yes" || value == "no")
                        {
                            needsAggregation = true;
                            categoryIndex = columns.IndexOf(col); // Use the boolean column as category
                            break;
                        }
                    }
                }
            }
            
            if (needsAggregation)
            {
                // Data needs aggregation - count occurrences by category
                var categoryCounts = new Dictionary<string, int>();
                
                foreach (var row in data)
                {
                    if (row is Dictionary<string, object> dict && categoryIndex < columns.Count)
                    {
                        var category = dict[columns[categoryIndex]]?.ToString() ?? "Unknown";
                        categoryCounts[category] = categoryCounts.GetValueOrDefault(category, 0) + 1;
                    }
                }
                
                labels = categoryCounts.Keys.ToList();
                series = categoryCounts.Values.Cast<object>().ToList();
                
                _logger.LogInformation("Aggregated pie data: labels={Labels}, series={Series}", 
                    string.Join(",", labels), string.Join(",", series));
            }
            else
            {
                // Check if data is already aggregated (has count column)
                if (firstRow != null && valueIndex < columns.Count)
                {
                    var firstValue = firstRow.ContainsKey(columns[valueIndex]) ? firstRow[columns[valueIndex]] : null;
                    
                    // If the value is numeric and greater than 1, it's likely already aggregated
                    if (firstValue != null && double.TryParse(firstValue.ToString(), out double numValue) && numValue > 1)
                    {
                        // Data is already aggregated, use as-is
                        foreach (var row in data)
                        {
                            if (row is Dictionary<string, object> dict)
                            {
                                if (categoryIndex < columns.Count)
                                {
                                    labels.Add(dict[columns[categoryIndex]]?.ToString() ?? "Unknown");
                                }
                                if (valueIndex < columns.Count && dict.ContainsKey(columns[valueIndex]))
                                {
                                    var value = dict[columns[valueIndex]];
                                    if (value != null && double.TryParse(value.ToString(), out double numericValue))
                                    {
                                        series.Add(numericValue);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Still needs aggregation even though not boolean
                        var categoryCounts = new Dictionary<string, int>();
                        
                        foreach (var row in data)
                        {
                            if (row is Dictionary<string, object> dict && categoryIndex < columns.Count)
                            {
                                var category = dict[columns[categoryIndex]]?.ToString() ?? "Unknown";
                                categoryCounts[category] = categoryCounts.GetValueOrDefault(category, 0) + 1;
                            }
                        }
                        
                        labels = categoryCounts.Keys.ToList();
                        series = categoryCounts.Values.Cast<object>().ToList();
                    }
                }
            }
        }
        else if (data.Count == 1)
        {
            // Single row with multiple columns - each column is a slice
            var row = data[0] as Dictionary<string, object>;
            if (row != null)
            {
                foreach (var kvp in row)
                {
                    labels.Add(kvp.Key);
                    if (kvp.Value != null && double.TryParse(kvp.Value.ToString(), out double numericValue))
                    {
                        series.Add(numericValue);
                    }
                }
            }
        }
        else
        {
            // Use the standard approach
            foreach (var row in data)
            {
                if (row is Dictionary<string, object> dict)
                {
                    if (categoryIndex < columns.Count)
                    {
                        labels.Add(dict[columns[categoryIndex]]?.ToString() ?? "Unknown");
                    }
                    if (valueIndex < columns.Count && dict.ContainsKey(columns[valueIndex]))
                    {
                        var value = dict[columns[valueIndex]];
                        if (value != null && double.TryParse(value.ToString(), out double numericValue))
                        {
                            series.Add(numericValue);
                        }
                    }
                }
            }
        }

        options.Labels = labels;
        options.Series = JsonSerializer.Serialize(series);
    }

    private void PopulateScatterChartData(
        ApexOptions options,
        VisualizationRecommendation recommendation,
        List<string> columns,
        List<object> data)
    {
        var points = new List<object>();
        var xIndex = recommendation.CategoryColumnIndex ?? 0;
        var yIndex = recommendation.ValueColumnIndices?.FirstOrDefault() ?? 1;

        foreach (var row in data)
        {
            if (row is Dictionary<string, object> dict)
            {
                if (xIndex < columns.Count && yIndex < columns.Count &&
                    dict.ContainsKey(columns[xIndex]) && dict.ContainsKey(columns[yIndex]))
                {
                    points.Add(new
                    {
                        x = dict[columns[xIndex]],
                        y = dict[columns[yIndex]]
                    });
                }
            }
        }

        var series = new[]
        {
            new
            {
                name = "Data Points",
                data = points
            }
        };

        options.Series = JsonSerializer.Serialize(series);
    }

    private VisualizationRecommendation CreateFallbackRecommendation(List<string> columns, List<object> data)
    {
        if (columns.Count == 1 && data.Count == 1)
        {
            return new VisualizationRecommendation
            {
                ResponseType = ResponseType.Text,
                Title = "Query Result",
                Reasoning = "Single value result",
                TextFormat = TextFormatType.Plain
            };
        }
        else if (columns.Count > 4 || data.Count > 50)
        {
            return new VisualizationRecommendation
            {
                ResponseType = ResponseType.Table,
                Title = "Query Results",
                Reasoning = "Multiple columns or many rows are best shown in a table"
            };
        }
        else
        {
            return new VisualizationRecommendation
            {
                ResponseType = ResponseType.Chart,
                ChartType = ChartType.Column,
                Title = "Data Visualization",
                Reasoning = "Default column chart for structured data",
                CategoryColumnIndex = 0,
                ValueColumnIndices = new List<int> { 1 }
            };
        }
    }

    private string FormatColumnName(string columnName)
    {
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
            if (IsNumeric(value))
                return ColumnDataType.Number;
            if (value is bool)
                return ColumnDataType.Boolean;
            if (value is DateTime)
                return ColumnDataType.DateTime;
        }
        
        return ColumnDataType.String;
    }

    private string FormatValue(object? value, TextFormatType formatType)
    {
        if (value == null)
            return "N/A";

        return formatType switch
        {
            TextFormatType.Number => value.ToString() ?? "0",
            TextFormatType.Currency => $"${Convert.ToDecimal(value):N2}",
            TextFormatType.Percentage => $"{Convert.ToDouble(value):F1}%",
            _ => value.ToString() ?? ""
        };
    }

    private bool IsNumeric(object value)
    {
        return value is int || value is long || value is float || value is double || value is decimal;
    }

    private string? InferUnit(string columnName)
    {
        var lowerName = columnName.ToLower();
        if (lowerName.Contains("count")) return "items";
        if (lowerName.Contains("amount") || lowerName.Contains("price") || lowerName.Contains("cost")) return "USD";
        if (lowerName.Contains("percent")) return "%";
        return null;
    }

    private UnifiedVisualizationResponse CreateFallbackVisualization(List<string> columns, List<object> data)
    {
        _logger.LogWarning("Creating fallback visualization");
        
        // Simple fallback logic
        if (columns.Count > 4 || data.Count > 50)
        {
            // Use table for complex data
            return new UnifiedVisualizationResponse
            {
                ResponseType = ResponseType.Table,
                Confidence = 0.3,
                SelectionReasoning = "Fallback: Complex data structure suggests table view",
                TableConfig = new TableVisualization
                {
                    Columns = columns.Select(col => new TableColumn
                    {
                        Key = col,
                        DisplayName = col,
                        DataType = ColumnDataType.String,
                        Sortable = true
                    }).ToList(),
                    EnablePagination = data.Count > 20,
                    EnableSorting = true
                }
            };
        }
        else
        {
            // Default to simple column chart
            return new UnifiedVisualizationResponse
            {
                ResponseType = ResponseType.Chart,
                Confidence = 0.3,
                SelectionReasoning = "Fallback: Default column chart visualization",
                ChartConfig = new ChartVisualization
                {
                    ChartType = ChartType.Column,
                    ApexChartOptions = CreateSimpleColumnChart(columns, data)
                }
            };
        }
    }

    private ApexOptions CreateSimpleColumnChart(List<string> columns, List<object> data)
    {
        var series = new List<object>();
        var labels = new List<string>();

        if (data.Any() && columns.Count >= 2)
        {
            foreach (var row in data)
            {
                if (row is Dictionary<string, object> dict)
                {
                    labels.Add(dict[columns[0]]?.ToString() ?? "");
                    if (dict.ContainsKey(columns[1]))
                    {
                        series.Add(dict[columns[1]]);
                    }
                }
            }
        }

        var seriesObject = new[] { new { name = columns.Count > 1 ? columns[1] : "Value", data = series } };
        return new ApexOptions
        {
            Chart = new Chart
            {
                Type = "bar",
                Height = 350,
                Toolbar = new Toolbar { Show = false }
            },
            Series = JsonSerializer.Serialize(seriesObject),
            Labels = labels,
            Title = new Title
            {
                Text = "Data Visualization",
                Align = "center",
                Style = new Style
                {
                    FontSize = "16px",
                    FontWeight = 600,
                    Color = "#263238"
                }
            },
            DataLabels = new DataLabels { Enabled = false },
            Grid = new Grid
            {
                BorderColor = "#e7e7e7",
                StrokeDashArray = 4
            },
            Legend = new Legend
            {
                Position = "bottom"
            }
        };
    }
}