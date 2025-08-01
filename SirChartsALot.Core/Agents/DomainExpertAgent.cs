using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SirChartsALot.Core.Configuration;
using SirChartsALot.Core.Models;
using SirChartsALot.Core.Services;

namespace SirChartsALot.Core.Agents;

public class DomainExpertAgent : IDomainExpertAgent
{
    private readonly Kernel _kernel;
    private readonly ISchemaIntrospectionService _schemaService;
    private readonly ILogger<DomainExpertAgent> _logger;
    private readonly AgentOptions _agentConfig;
    private readonly string _promptTemplate = """
        You are a database schema expert helping users query their data. Your role is to:

        1. Understand the user's natural language query
        2. Identify which tables (maximum 5) are relevant FROM THE AVAILABLE TABLES BELOW
        3. Provide a brief analysis of what the user wants

        IMPORTANT: You can ONLY select from these available tables:
        {{$schema}}

        For the user query: "{{$query}}"

        Rules:
        - ONLY select tables that actually exist in the list above
        - If no tables seem relevant, select the most likely ones and explain the limitation
        - Be generous in table selection if the query is ambiguous
        - Table names must match EXACTLY as shown above (case-sensitive)
        - IMPORTANT: If you see foreign key relationships (FK ->), also include the referenced parent tables
        - When looking for user/person attributes (like filing status), check ALL tables that might contain user data
        - Better to include too many tables than too few - the SQL expert will figure out what's needed

        Respond with valid JSON only:
        {
          "intent": "what the user wants to achieve",
          "tables": ["exact_table_name_from_list"],
          "relationships": "how tables connect",
          "complexity": "Simple|Medium|Complex"
        }

        If the available tables don't match the query well, still select the best matches and note this in the intent.
        """;

    public DomainExpertAgent(
        IOptions<AzureOpenAIOptions> azureOptions,
        IOptions<AgentsConfiguration> agentOptions,
        ISchemaIntrospectionService schemaService,
        ILogger<DomainExpertAgent> logger)
    {
        _schemaService = schemaService;
        _logger = logger;
        _agentConfig = agentOptions.Value.DomainExpert;
        
        var azureConfig = azureOptions.Value;
        _logger.LogInformation("Initializing Domain Expert Agent - Model: {Model}, Endpoint: {Endpoint}", 
            _agentConfig.Model, azureConfig.Endpoint);
        
        // Validate configuration
        if (string.IsNullOrWhiteSpace(azureConfig.Endpoint))
        {
            throw new InvalidOperationException("Azure OpenAI endpoint is not configured. Please check your appsettings.json or appsettings.Development.json");
        }
        
        if (string.IsNullOrWhiteSpace(azureConfig.ApiKey))
        {
            throw new InvalidOperationException("Azure OpenAI API key is not configured. Please check your appsettings.json or appsettings.Development.json");
        }

        var builder = Kernel.CreateBuilder();
        
        // Configure Azure OpenAI with configured model
        builder.AddAzureOpenAIChatCompletion(
            deploymentName: _agentConfig.Model,
            endpoint: azureConfig.Endpoint,
            apiKey: azureConfig.ApiKey);

        _kernel = builder.Build();
    }

    public async Task<DomainAnalysis> AnalyzeQueryAsync(string userQuery)
    {
        _logger.LogInformation("Domain Expert analyzing query: {Query}", userQuery);
        
        try
        {
            // Get high-level schema (table names + descriptions only)
            var highLevelSchema = await _schemaService.GetHighLevelSchemaAsync();
            
            _logger.LogInformation("Domain Expert received schema:\n{Schema}", highLevelSchema);
            
            // Create the prompt
            var promptSettings = new OpenAIPromptExecutionSettings
            {
                // O-series models (o1, o3, o4) require temperature of 1.0
                Temperature = (_agentConfig.Model.StartsWith("o1") || _agentConfig.Model.StartsWith("o3") || _agentConfig.Model.StartsWith("o4")) 
                    ? 1.0 : _agentConfig.Temperature,
                ResponseFormat = "json_object" // Ensure JSON response
            };
            
            // Don't set MaxTokens for o3/o4 models as they use max_completion_tokens
            if (!_agentConfig.Model.Contains("o3") && !_agentConfig.Model.Contains("o4"))
            {
                promptSettings.MaxTokens = _agentConfig.MaxTokens;
            }
            
            var arguments = new KernelArguments
            {
                ["schema"] = highLevelSchema,
                ["query"] = userQuery
            };
            
            // Execute the prompt
            var promptFunction = KernelFunctionFactory.CreateFromPrompt(
                _promptTemplate,
                executionSettings: promptSettings);
            
            var result = await _kernel.InvokeAsync(
                promptFunction,
                arguments);
            
            var jsonResponse = result.ToString();
            _logger.LogDebug("Domain Expert response: {Response}", jsonResponse);
            
            // Parse the JSON response
            var analysis = JsonSerializer.Deserialize<DomainAnalysis>(
                jsonResponse, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (analysis == null)
            {
                throw new InvalidOperationException("Failed to parse domain analysis response");
            }
            
            // Validate table count
            if (analysis.Tables.Count > 5)
            {
                _logger.LogWarning("Domain Expert returned more than 5 tables, truncating to 5");
                analysis.Tables = analysis.Tables.Take(5).ToList();
            }
            
            // Validate that tables actually exist in the schema
            var schemaCache = await _schemaService.GetSchemaCacheAsync();
            var validTables = new List<string>();
            var invalidTables = new List<string>();
            
            foreach (var tableName in analysis.Tables)
            {
                // Try case-insensitive match
                var actualTable = schemaCache.Tables.Keys
                    .FirstOrDefault(k => k.Equals(tableName.ToUpper(), StringComparison.OrdinalIgnoreCase));
                
                if (actualTable != null)
                {
                    validTables.Add(schemaCache.Tables[actualTable].TableName);
                }
                else
                {
                    invalidTables.Add(tableName);
                }
            }
            
            if (invalidTables.Any())
            {
                _logger.LogWarning("Domain Expert selected non-existent tables: {InvalidTables}", 
                    string.Join(", ", invalidTables));
            }
            
            // Update analysis with valid tables only
            analysis.Tables = validTables;
            
            // Auto-include parent tables referenced by foreign keys
            var additionalTables = new HashSet<string>();
            foreach (var tableName in analysis.Tables)
            {
                var table = schemaCache.Tables.Values.FirstOrDefault(t => 
                    t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
                    
                if (table != null)
                {
                    // Check all columns for foreign key references
                    foreach (var column in table.Columns.Where(c => c.IsForeignKey && !string.IsNullOrEmpty(c.ReferencedTable)))
                    {
                        if (!string.IsNullOrEmpty(column.ReferencedTable) && 
                            !analysis.Tables.Contains(column.ReferencedTable, StringComparer.OrdinalIgnoreCase))
                        {
                            additionalTables.Add(column.ReferencedTable);
                            _logger.LogInformation("Auto-including parent table {ParentTable} referenced by {Table}.{Column}", 
                                column.ReferencedTable, tableName, column.ColumnName);
                        }
                    }
                }
            }
            
            // Add the parent tables
            foreach (var parentTable in additionalTables)
            {
                var actualTable = schemaCache.Tables.Keys
                    .FirstOrDefault(k => k.Equals(parentTable.ToUpper(), StringComparison.OrdinalIgnoreCase));
                    
                if (actualTable != null && analysis.Tables.Count < 5)
                {
                    analysis.Tables.Add(schemaCache.Tables[actualTable].TableName);
                }
            }
            
            if (!analysis.Tables.Any())
            {
                _logger.LogError("No valid tables identified for query: {Query}", userQuery);
                throw new InvalidOperationException(
                    $"Could not identify any valid tables for your query. Available tables are: {string.Join(", ", schemaCache.Tables.Values.Select(t => t.TableName))}");
            }
            
            _logger.LogInformation("Domain Expert identified {TableCount} valid tables: {Tables}", 
                analysis.Tables.Count, string.Join(", ", analysis.Tables));
            
            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Domain Expert analysis");
            
            // Return a simple fallback analysis
            return new DomainAnalysis
            {
                Intent = "Unable to analyze query due to error",
                Tables = new List<string>(),
                Relationships = "Unknown",
                Complexity = "Complex"
            };
        }
    }
}