using SirChartsALot.Api.Hubs;
using SirChartsALot.Api.Services;
using SirChartsALot.Core.Agents;
using SirChartsALot.Core.Configuration;
using SirChartsALot.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Log the environment
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"ContentRoot: {builder.Environment.ContentRootPath}");
Console.WriteLine($"IsDevelopment: {builder.Environment.IsDevelopment()}");

// List configuration sources
Console.WriteLine("Configuration sources:");
foreach (var source in builder.Configuration.Sources)
{
    Console.WriteLine($"  - {source.GetType().Name}");
}

// Add services to the container.
builder.Services.AddSignalR(options =>
{
    // Increase timeouts to handle o4-mini's longer processing times (30-45 seconds)
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    
    // Enable detailed errors in development
    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors = true;
    }
});
builder.Services.AddMemoryCache();

// Configure Azure OpenAI
var azureOpenAISection = builder.Configuration.GetSection(AzureOpenAIOptions.SectionName);
Console.WriteLine($"\nAzureOpenAI Configuration:");
Console.WriteLine($"  Endpoint from config: '{azureOpenAISection["Endpoint"]}'");
Console.WriteLine($"  ApiKey exists: {!string.IsNullOrEmpty(azureOpenAISection["ApiKey"])}");
Console.WriteLine($"  DeploymentName: '{azureOpenAISection["DeploymentName"]}'");

// Try to get the values directly
var endpoint = builder.Configuration["AzureOpenAI:Endpoint"];
var apiKey = builder.Configuration["AzureOpenAI:ApiKey"];
Console.WriteLine($"\nDirect configuration access:");
Console.WriteLine($"  AzureOpenAI:Endpoint = '{endpoint}'");
Console.WriteLine($"  AzureOpenAI:ApiKey exists = {!string.IsNullOrEmpty(apiKey)}");

builder.Services.Configure<AzureOpenAIOptions>(azureOpenAISection);

// Configure Agents
builder.Services.Configure<AgentsConfiguration>(
    builder.Configuration.GetSection(AgentsConfiguration.SectionName));

// Register Core services
builder.Services.AddSingleton<ISchemaIntrospectionService, SchemaIntrospectionService>();
builder.Services.AddScoped<ISqlExecutionService, SqlExecutionService>();

// Register Agents
builder.Services.AddScoped<IDomainExpertAgent, DomainExpertAgent>();
builder.Services.AddScoped<ISqlQueryExpertAgent, SqlQueryExpertAgent>();

// Add Semantic Kernel service - Always use real service
builder.Services.AddSingleton<ISemanticKernelService, SemanticKernelService>();

// Add Enhanced Visualization Service
builder.Services.AddSingleton<IEnhancedVisualizationService, EnhancedVisualizationService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        builder =>
        {
            builder.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200", "http://10.255.255.254:4200")
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors("AllowAngularApp");

app.MapHub<ChartHub>("/charthub");
app.MapHub<DataInsightHub>("/datainsighthub");

// Pre-load schema cache on startup - MUST complete before accepting requests
using (var scope = app.Services.CreateScope())
{
    var schemaService = scope.ServiceProvider.GetRequiredService<ISchemaIntrospectionService>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    
    try
    {
        var connectionString = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("WARNING: No database connection string configured!");
            Console.WriteLine("Please add ConnectionStrings:DefaultConnection to your appsettings.json");
            Console.WriteLine("The system will run but cannot process queries without a database.");
        }
        else
        {
            Console.WriteLine("Pre-loading database schema cache...");
            var schemaLoadTask = schemaService.RefreshSchemaAsync();
            
            // Wait for schema to load with timeout
            if (await Task.WhenAny(schemaLoadTask, Task.Delay(TimeSpan.FromSeconds(30))) == schemaLoadTask)
            {
                await schemaLoadTask; // Ensure any exceptions are thrown
                var cache = await schemaService.GetSchemaCacheAsync();
                Console.WriteLine($"Schema cache loaded successfully with {cache.Tables.Count} tables");
            }
            else
            {
                Console.WriteLine("WARNING: Schema loading timed out after 30 seconds");
                Console.WriteLine("The system will continue but may have incomplete schema information");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR loading schema: {ex.Message}");
        Console.WriteLine("The system will run but cannot process queries without valid schema.");
        // Don't throw - let the app start but queries will fail with clear errors
    }
}

app.Run();
