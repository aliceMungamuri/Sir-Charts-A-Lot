using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SirChartsALot.Core.Models.HttpHandlers;

public sealed class LoggingHandler(HttpMessageHandler innerHandler, ILoggerFactory output) : DelegatingHandler(innerHandler)
{
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new() { WriteIndented = true };

    private readonly ILoggerFactory _loggerFactory = output;
    private ILogger<LoggingHandler> _output => _loggerFactory.CreateLogger<LoggingHandler>();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        
        _output.LogInformation(request.RequestUri?.ToString());
        if (request.Content is not null)
        {
            var content = await request.Content.ReadAsStringAsync(cancellationToken);
            _output.LogInformation("=== REQUEST ===");
            try
            {
                string formattedContent = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(content), s_jsonSerializerOptions);
                _output.LogInformation(formattedContent);
            }
            catch (JsonException)
            {
                _output.LogInformation(content);
            }
            _output.LogInformation(string.Empty);
        }

        // Call the next handler in the pipeline
        var response = await base.SendAsync(request, cancellationToken);

        if (response.Content is not null)
        {
            // Log the response details
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _output.LogInformation("=== RESPONSE ===");
            _output.LogInformation(responseContent);
            _output.LogInformation(string.Empty);
        }

        return response;
    }
}