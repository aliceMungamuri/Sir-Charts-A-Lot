namespace SirChartsALot.Core.Models;

public class SchemaCache
{
    public Dictionary<string, TableInfo> Tables { get; set; } = new();
    public Dictionary<string, RelationshipInfo> Relationships { get; set; } = new();
    public DateTime LastRefreshed { get; set; }
}

public class RelationshipInfo
{
    public string FromTable { get; set; } = string.Empty;
    public string FromColumn { get; set; } = string.Empty;
    public string ToTable { get; set; } = string.Empty;
    public string ToColumn { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = "OneToMany";
}