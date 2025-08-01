namespace SirChartsALot.Core.Models;

public class TableInfo
{
    public string TableName { get; set; } = string.Empty;
    public string Schema { get; set; } = "dbo";
    public string Description { get; set; } = string.Empty;
    public List<ColumnInfo> Columns { get; set; } = new();
    public List<string> SampleValues { get; set; } = new();
}

public class ColumnInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public string? ReferencedTable { get; set; }
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
}