using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SirChartsALot.Core.Configuration;
using System.Net.Http;
using System.Text.Json;
using OpenAI.Chat;
using SirChartsALot.Api.Models;
using SirChartsALot.Core.Models;

namespace SirChartsALot.Api.Services;

public interface ISemanticKernelService
{
    Task<string> GetChatResponseAsync(string userMessage);
    Kernel GetKernel();
    Task<VisualizationData> RunMiniVizAgent(List<string> columns, List<object> data);
}

public class SemanticKernelService : ISemanticKernelService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<SemanticKernelService> _logger;
    private readonly AzureOpenAIOptions _azureOptions;

    public SemanticKernelService(IOptions<AzureOpenAIOptions> options, ILogger<SemanticKernelService> logger)
    {
        _logger = logger;
        var azureOptions = options.Value;
        _azureOptions = azureOptions;
        _logger.LogInformation("Initializing Semantic Kernel with Azure OpenAI - Endpoint: {Endpoint}, Deployment: {Deployment}", 
            azureOptions.Endpoint, azureOptions.DeploymentName);
        
        // Create HTTP client with custom timeout and proxy bypass
        var httpClientHandler = new HttpClientHandler();
        
        // Try to bypass proxy for Azure endpoints
        httpClientHandler.UseProxy = false;
        
        var httpClient = new HttpClient(httpClientHandler)
        {
            Timeout = TimeSpan.FromSeconds(30) // Reduce from default 100s to 30s
        };
        
        // Build the kernel with Azure OpenAI
        var builder = Kernel.CreateBuilder();
        
        builder.AddAzureOpenAIChatCompletion(
            deploymentName: azureOptions.DeploymentName,
            endpoint: azureOptions.Endpoint,
            apiKey: azureOptions.ApiKey,
            serviceId: "AzureOpenAI",
            httpClient: httpClient
        );
        
        _kernel = builder.Build();
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
        
        _logger.LogInformation("Semantic Kernel initialized successfully");
    }
    public async Task<VisualizationData> RunMiniVizAgent(List<string> columns, List<object> data)
    {
        var obj = new {Columns = columns, Data = data};
        _logger.LogInformation("Running MiniViz Agent with data: {Data}", 
            JsonSerializer.Serialize(obj, new JsonSerializerOptions(){WriteIndented = true}));
        var prompt = """
                     Use your knowledge of the Apex Charts to generate apex chart options json from provided data using the provided schema.
                     Do not include any comments or explanations inside the json. Properties and values only.
                     Using the columns and data provided, generate a visualization that best represents the data by populating the series and labels of the options.
                     **Columns**: 
                     {{ $columns }}
                     **Data**:
                     {{ $data }}
                     """;
        var kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion("o4-mini", _azureOptions.Endpoint, _azureOptions.ApiKey).Build();
        var settings = new OpenAIPromptExecutionSettings() { ResponseFormat = typeof(VisualizationData)};
        var args = new KernelArguments(settings) { ["columns"] = JsonSerializer.Serialize(columns), ["data"] = JsonSerializer.Serialize(data) };
        var response = await kernel.InvokePromptAsync<string>(prompt, args);
        _logger.LogInformation("Received response from MiniViz Agent: {Response}", response);
        
        if (string.IsNullOrEmpty(response))
        {
            throw new InvalidOperationException("Received empty response from MiniViz Agent");
        }
        
        var visualization = JsonSerializer.Deserialize<VisualizationData>(response);
        
        if (visualization == null)
        {
            throw new InvalidOperationException("Failed to deserialize visualization response");
        }

        return visualization;

    }
    public async Task<string> GetChatResponseAsync(string userMessage)
    {
        try
        {
            _logger.LogInformation("Starting chat completion for message: {Message}", userMessage);
            
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are Sir Charts-a-lot, an AI assistant specialized in data analysis and visualization. Be helpful and concise.");
            chatHistory.AddUserMessage(userMessage);

            _logger.LogInformation("Calling Azure OpenAI chat service...");
            
            var response = await _chatService.GetChatMessageContentAsync(
                chatHistory,
                kernel: _kernel
            );
            
            _logger.LogInformation("Received response from Azure OpenAI");
            
            return response.Content ?? "I apologize, but I couldn't generate a response.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response from Semantic Kernel. Message: {Message}, Type: {Type}", ex.Message, ex.GetType().Name);
            return $"I apologize, but I'm having trouble processing your request. Error: {ex.Message}";
        }
    }
    
    public Kernel GetKernel()
    {
        return _kernel;
    }
}