using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SirChartsALot.Core.Models;

/// <summary>
/// Simplified visualization recommendation from AI
/// </summary>
public class VisualizationRecommendation
{
    [Description("The type of visualization to display")]
    public ResponseType ResponseType { get; set; }
    
    [Description("Specific chart type if ResponseType is Chart")]
    public ChartType? ChartType { get; set; }
    
    [Description("Title for the visualization")]
    public required string Title { get; set; }
    
    [Description("Explanation of why this visualization was chosen")]
    public required string Reasoning { get; set; }
    
    [Description("For charts: index of column to use for categories/labels (usually 0)")]
    public int? CategoryColumnIndex { get; set; }
    
    [Description("For charts: indices of columns to use for values")]
    public List<int>? ValueColumnIndices { get; set; }
    
    [Description("For text responses: the display format")]
    public TextFormatType? TextFormat { get; set; }
    
    [Description("Human-readable insight about the data")]
    public string? DataInsight { get; set; }
}

/// <summary>
/// Unified response model for all visualization types including charts, tables, and text
/// </summary>
public class UnifiedVisualizationResponse
{
    [Description("The type of response to display")]
    public ResponseType ResponseType { get; set; }
    
    [Description("Confidence score (0-1) for the selected visualization type")]
    public double Confidence { get; set; }
    
    [Description("Explanation of why this visualization type was chosen")]
    public required string SelectionReasoning { get; set; }
    
    [Description("Chart configuration when ResponseType is Chart")]
    public ChartVisualization? ChartConfig { get; set; }
    
    [Description("Table configuration when ResponseType is Table")]
    public TableVisualization? TableConfig { get; set; }
    
    [Description("Text response when ResponseType is Text or Mixed")]
    public TextVisualization? TextConfig { get; set; }
    
    [Description("Additional visualizations when ResponseType is Mixed")]
    public List<SecondaryVisualization>? SecondaryVisualizations { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResponseType
{
    Chart,
    Table,
    Text,
    Mixed
}

/// <summary>
/// Enhanced chart visualization with support for all ApexCharts types
/// </summary>
public class ChartVisualization
{
    [Description("The specific chart type from ApexCharts")]
    public ChartType ChartType { get; set; }
    
    [Description(VisualizationData.Examples)]
    public required ApexOptions ApexChartOptions { get; set; }
    
    //[Description("Data transformation hints for complex chart types")]
    //public DataTransformation? DataTransformation { get; set; }
    
    //[Description("Fallback chart type if primary fails")]
    //public ChartType? FallbackChartType { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChartType
{
    Line,
    Area,
    Column,
    Bar,
    Pie,
    Donut,
    RadialBar,
    Scatter,
    Bubble,
    Heatmap,
    Treemap,
    Candlestick,
    BoxPlot,
    Radar,
    PolarArea,
    RangeBar,
    Funnel
}

/// <summary>
/// Table visualization configuration
/// </summary>
public class TableVisualization
{
    [Description("Column definitions for the table")]
    public required List<TableColumn> Columns { get; set; }
    
    [Description("Whether to enable pagination")]
    public bool EnablePagination { get; set; }
    
    [Description("Initial page size")]
    public int PageSize { get; set; } = 10;
    
    [Description("Whether to enable sorting")]
    public bool EnableSorting { get; set; }
    
    [Description("Whether to enable filtering")]
    public bool EnableFiltering { get; set; }
    
    [Description("Whether to enable export functionality")]
    public bool EnableExport { get; set; }
    
    [Description("Conditional formatting rules")]
    public List<ConditionalFormat>? ConditionalFormats { get; set; }
}

public class TableColumn
{
    [Description("Column identifier matching the data key")]
    public required string Key { get; set; }
    
    [Description("Display name for the column")]
    public required string DisplayName { get; set; }
    
    [Description("Data type of the column")]
    public ColumnDataType DataType { get; set; }
    
    [Description("Format string for the column (e.g., 'currency', 'percentage', 'date')")]
    public string? Format { get; set; }
    
    [Description("Column width in pixels or percentage")]
    public string? Width { get; set; }
    
    [Description("Whether this column is sortable")]
    public bool Sortable { get; set; } = true;
    
    [Description("Whether to show a sparkline for numeric data")]
    public bool ShowSparkline { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ColumnDataType
{
    String,
    Number,
    Currency,
    Percentage,
    Date,
    DateTime,
    Boolean,
    Link
}

/// <summary>
/// Text response configuration for single values or summaries
/// </summary>
public class TextVisualization
{
    [Description("The main text content")]
    public required string Content { get; set; }
    
    [Description("Format type for the text")]
    public TextFormatType FormatType { get; set; }
    
    [Description("Whether this is a single value response")]
    public bool IsSingleValue { get; set; }
    
    [Description("Metadata for single value responses")]
    public SingleValueMetadata? SingleValueMetadata { get; set; }
    
    [Description("Markdown formatting if applicable")]
    public bool UseMarkdown { get; set; }
    
    [Description("Key insights or highlights")]
    public List<string>? Highlights { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TextFormatType
{
    Plain,
    Number,
    Currency,
    Percentage,
    Markdown,
    Summary
}

public class SingleValueMetadata
{
    [Description("The actual numeric value if applicable")]
    public double? NumericValue { get; set; }
    
    [Description("Unit of measurement")]
    public string? Unit { get; set; }
    
    [Description("Comparison with previous period")]
    public ComparisonData? Comparison { get; set; }
    
    [Description("Visual indicator (up/down arrow, etc.)")]
    public string? Indicator { get; set; }
}

public class ComparisonData
{
    public double PreviousValue { get; set; }
    public double ChangeAmount { get; set; }
    public double ChangePercentage { get; set; }
    public required string Direction { get; set; } // "up", "down", "stable"
}

/// <summary>
/// Data transformation hints for complex visualizations
/// </summary>
public class DataTransformation
{
    [Description("Type of transformation needed")]
    public TransformationType TransformationType { get; set; }

    [Description("Specific configuration json for the transformation")]
    public string? Config { get; set; }

    public object? GetConfigObject()
    {
        if (string.IsNullOrWhiteSpace(Config))
            return null;
        return JsonSerializer.Deserialize<object>(Config);
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransformationType
{
    None,
    Pivot,
    Aggregate,
    Hierarchical,
    Matrix,
    TimeSeries,
    OHLC
}

/// <summary>
/// Secondary visualization for mixed responses
/// </summary>
public class SecondaryVisualization
{
    [Description("Order of display")]
    public int Order { get; set; }
    
    [Description("Type of secondary visualization")]
    public ResponseType ResponseType { get; set; }
    
    [Description("Configuration json based on type")]
    public required string Configuration { get; set; }
    public object? GetConfigurationObject()
    {
        if (string.IsNullOrWhiteSpace(Configuration))
            return null;
        return JsonSerializer.Deserialize<object>(Configuration);
    }
}

/// <summary>
/// Conditional formatting for tables
/// </summary>
public class ConditionalFormat
{
    [Description("Column to apply formatting to")]
    public required string ColumnKey { get; set; }
    
    [Description("Condition type")]
    public ConditionType Condition { get; set; }
    
    [Description("Value json to compare against")]
    public required string Value { get; set; }
    
    [Description("CSS class or style to apply")]
    public required string Style { get; set; }
    public object? GetValueObject()
    {
        if (string.IsNullOrWhiteSpace(Value))
            return null;
        return JsonSerializer.Deserialize<object>(Value);
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConditionType
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Contains,
    StartsWith,
    EndsWith
}