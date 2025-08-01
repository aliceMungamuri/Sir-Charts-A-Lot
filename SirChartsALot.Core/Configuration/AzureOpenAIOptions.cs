namespace SirChartsALot.Core.Configuration;

public class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";
    
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2024-10-01-preview";
}

public class AgentOptions
{
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 500;
}

public class AgentsConfiguration
{
    public const string SectionName = "Agents";
    
    public AgentOptions DomainExpert { get; set; } = new();
    public AgentOptions SqlExpert { get; set; } = new();
    public AgentOptions Visualization { get; set; } = new();
}