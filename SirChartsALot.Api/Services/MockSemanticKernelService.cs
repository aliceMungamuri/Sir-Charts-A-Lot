using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using SirChartsALot.Api.Models;
using SirChartsALot.Core.Configuration;
using SirChartsALot.Core.Models;

namespace SirChartsALot.Api.Services;

public class MockSemanticKernelService : ISemanticKernelService
{
    private readonly ILogger<MockSemanticKernelService> _logger;
    private readonly Kernel _kernel;

    public MockSemanticKernelService(ILogger<MockSemanticKernelService> logger)
    {
        _logger = logger;
        _kernel = Kernel.CreateBuilder().Build(); // Empty kernel for mock
        _logger.LogWarning("Using MOCK Semantic Kernel Service - No real AI responses!");
    }

    public async Task<string> GetChatResponseAsync(string userMessage)
    {
        _logger.LogInformation("Mock response for: {Message}", userMessage);
        
        // Simulate processing delay
        await Task.Delay(1000);
        
        // Return mock responses based on keywords
        if (userMessage.ToLower().Contains("revenue") || userMessage.ToLower().Contains("sales"))
        {
            return "Based on the mock data, I can see that revenue has been trending upward over the past 6 months, with December showing the highest revenue at $78,000. The average monthly revenue is approximately $63,000.";
        }
        
        if (userMessage.ToLower().Contains("hello") || userMessage.ToLower().Contains("who are you"))
        {
            return "Hello! I'm Sir Charts-a-lot (running in mock mode). I'm an AI assistant specialized in data analysis and visualization. In production, I'll help you query databases using natural language and create insightful charts!";
        }
        
        return $"[MOCK RESPONSE] I received your query: '{userMessage}'. In production, I would analyze this with Azure OpenAI and potentially generate SQL queries and visualizations.";
    }
    
    public Kernel GetKernel()
    {
        return _kernel;
    }

    public Task<VisualizationData> RunMiniVizAgent(List<string> columns, List<object> data)
    {
        throw new NotImplementedException();
    }
}