namespace SirChartsALot.Api.Models
{
    public record ThinkingMessage(string Stage, string Message);
    
    public record SqlMessage(string Query);
    
    public record SqlGeneratedMessage(
        string Query,
        string[] TablesUsed,
        string[] ColumnsSelected,
        bool IsParameterized
    );
    
    public record DataStreamMessage(object[] Data, string[] Columns, bool IsComplete);
    
    public record VisualizationMessage(string Type, string Title, string VizTag, string[]? Columns = null);
    
    public record TimelineEventMessage(
        string Id,
        string Agent,
        string Stage,
        string Message,
        string Status,
        string[]? Details = null
    );
    
    public record ThinkingCompleteMessage(int Duration);
}