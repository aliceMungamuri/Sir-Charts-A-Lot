using SirChartsALot.Core.Models;

namespace SirChartsALot.Api.Models;

/// <summary>
/// Enhanced visualization message that supports all response types
/// </summary>
public record UnifiedVisualizationMessage(
    ResponseType ResponseType,
    string Title,
    double Confidence,
    string SelectionReasoning,
    string SerializedConfig,  // JSON serialized config based on ResponseType
    string[]? Columns = null,
    int? TotalRowCount = null
);

/// <summary>
/// Table data message for streaming table rows
/// </summary>
public record TableDataMessage(
    object[] Rows,
    TableColumn[] ColumnDefinitions,
    bool IsComplete,
    int TotalRowCount,
    int CurrentPage = 1,
    int PageSize = 10
);

/// <summary>
/// Text response message for single values or summaries
/// </summary>
public record TextResponseMessage(
    string Content,
    TextFormatType FormatType,
    bool IsSingleValue,
    SingleValueMetadata? Metadata = null,
    List<string>? Highlights = null
);

/// <summary>
/// Mixed response message container
/// </summary>
public record MixedResponseMessage(
    TextResponseMessage? TextPart,
    UnifiedVisualizationMessage? VisualizationPart,
    int DisplayOrder
);