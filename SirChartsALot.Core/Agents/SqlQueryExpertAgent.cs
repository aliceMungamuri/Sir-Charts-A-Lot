using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SirChartsALot.Core.Configuration;
using SirChartsALot.Core.Models;
using SirChartsALot.Core.Services;

namespace SirChartsALot.Core.Agents;

public class SqlQueryExpertAgent : ISqlQueryExpertAgent
{
    private readonly Kernel _kernel;
    private readonly ISchemaIntrospectionService _schemaService;
    private readonly ILogger<SqlQueryExpertAgent> _logger;
    private readonly AgentOptions _agentConfig;
    private readonly string _promptTemplate = """
        You are a T-SQL expert. Generate ONLY the SQL query based on this information:

        User Intent: {{$intent}}
        Tables to use: {{$tables}}

        Table Schemas:
        {{$schema}}

        Rules:
        1. Generate ONLY valid T-SQL syntax
        2. Use proper JOIN syntax
        3. Include appropriate WHERE clauses
        4. Add TOP 1000 to prevent large result sets
        5. Use clear column aliases
        6. NO explanations - just the query
        7. Use proper date formatting for SQL Server
        8. Ensure all columns in SELECT are either in GROUP BY or aggregate functions
        9. NEVER use SQL reserved keywords as table aliases (avoid IF, ELSE, CASE, END, etc.)
        10. Use meaningful table aliases (first 3 letters of table name, or logical abbreviation)
        11. ONLY use columns that exist in the provided schema above
        12. IMPORTANT: When the intent asks for "users who..." or "how many users...", generate a COUNT of users, not aggregate financial data
        13. When comparing user groups (e.g., "users who X vs users who Y"), return counts or percentages of users in each group

        If the query cannot be generated, respond with:
        ERROR: [Specific reason, including what columns are available if relevant]

        Generate the SQL query now:
        """;

    public SqlQueryExpertAgent(
        IOptions<AzureOpenAIOptions> azureOptions,
        IOptions<AgentsConfiguration> agentOptions,
        ISchemaIntrospectionService schemaService,
        ILogger<SqlQueryExpertAgent> logger)
    {
        _schemaService = schemaService;
        _logger = logger;
        _agentConfig = agentOptions.Value.SqlExpert;
        
        var azureConfig = azureOptions.Value;
        _logger.LogInformation("Initializing SQL Query Expert Agent - Model: {Model}, Endpoint: {Endpoint}", 
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

    public async Task<string> GenerateSqlAsync(DomainAnalysis analysis, string userQuery)
    {
        _logger.LogInformation("SQL Expert generating query for intent: {Intent}", analysis.Intent);
        
        try
        {
            // Get detailed schema for only the selected tables
            var detailedSchema = await _schemaService.GetDetailedSchemaAsync(analysis.Tables.ToArray());
            
            _logger.LogInformation("SQL Expert received schema for tables {Tables}:\n{Schema}", 
                string.Join(", ", analysis.Tables), detailedSchema);
            
            // Create the prompt
            var promptSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = _agentConfig.Temperature
            };
            
            // Don't set MaxTokens for o3/o4 models as they use max_completion_tokens
            if (!_agentConfig.Model.Contains("o3") && !_agentConfig.Model.Contains("o4"))
            {
                promptSettings.MaxTokens = _agentConfig.MaxTokens;
            }
            
            var arguments = new KernelArguments
            {
                ["intent"] = analysis.Intent,
                ["tables"] = string.Join(", ", analysis.Tables),
                ["schema"] = detailedSchema
            };
            
            // Execute the prompt
            var promptFunction = KernelFunctionFactory.CreateFromPrompt(
                _promptTemplate,
                executionSettings: promptSettings);
            
            var result = await _kernel.InvokeAsync(
                promptFunction,
                arguments);
            
            var sqlQuery = result.ToString().Trim();
            _logger.LogInformation("SQL Expert generated raw query: {Query}", sqlQuery);
            
            // Check for error response
            if (sqlQuery.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("SQL Expert returned error: {Error}", sqlQuery);
                throw new InvalidOperationException(sqlQuery);
            }
            
            // Validate and clean the SQL
            sqlQuery = ValidateAndCleanSql(sqlQuery);
            _logger.LogInformation("SQL Expert validated query: {Query}", sqlQuery);
            
            return sqlQuery;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SQL Expert generation");
            throw;
        }
    }

    private string ValidateAndCleanSql(string sql)
    {
        // Remove any markdown code blocks if present
        sql = Regex.Replace(sql, @"```sql\s*", "", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"```\s*", "");
        sql = sql.Trim();
        
        // Basic validation
        if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Generated SQL must be a SELECT statement");
        }
        
        // Ensure TOP clause is present
        if (!Regex.IsMatch(sql, @"\bTOP\s+\d+\b", RegexOptions.IgnoreCase))
        {
            // Add TOP 1000 after SELECT
            sql = Regex.Replace(sql, @"^SELECT\s+", "SELECT TOP 1000 ", RegexOptions.IgnoreCase);
        }
        
        // Check for potentially dangerous operations
        var dangerousKeywords = new[] { "DROP", "DELETE", "UPDATE", "INSERT", "ALTER", "CREATE", "TRUNCATE", "EXEC", "EXECUTE" };
        foreach (var keyword in dangerousKeywords)
        {
            if (Regex.IsMatch(sql, $@"\b{keyword}\b", RegexOptions.IgnoreCase))
            {
                throw new InvalidOperationException($"SQL contains forbidden operation: {keyword}");
            }
        }
        
        // Basic SQL injection pattern detection
        // Allow single semicolon at the end, but not multiple statements
        var semicolonCount = sql.Count(c => c == ';');
        if (semicolonCount > 1 || (semicolonCount == 1 && !sql.TrimEnd().EndsWith(";")))
        {
            throw new InvalidOperationException("SQL contains multiple statements");
        }
        
        // Check for SQL comment injection (but allow legitimate comments)
        if (sql.Contains("--") && sql.Contains("'"))
        {
            // Potential SQL injection via comments after quotes
            throw new InvalidOperationException("SQL contains potentially dangerous comment pattern");
        }
        
        return sql;
    }
}