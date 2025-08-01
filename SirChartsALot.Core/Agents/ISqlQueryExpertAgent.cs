using SirChartsALot.Core.Models;

namespace SirChartsALot.Core.Agents;

public interface ISqlQueryExpertAgent
{
    Task<string> GenerateSqlAsync(DomainAnalysis analysis, string userQuery);
}