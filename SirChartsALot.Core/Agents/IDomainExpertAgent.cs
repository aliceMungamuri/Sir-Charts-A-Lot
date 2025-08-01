using SirChartsALot.Core.Models;

namespace SirChartsALot.Core.Agents;

public interface IDomainExpertAgent
{
    Task<DomainAnalysis> AnalyzeQueryAsync(string userQuery);
}