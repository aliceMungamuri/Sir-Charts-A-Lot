namespace SirChartsALot.Core.Models;

public class DomainAnalysis
{
    public string Intent { get; set; } = string.Empty;
    public List<string> Tables { get; set; } = new();
    public string Relationships { get; set; } = string.Empty;
    public string Complexity { get; set; } = "Simple";
}