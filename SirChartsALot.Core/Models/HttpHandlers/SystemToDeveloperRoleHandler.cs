using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace SirChartsALot.Core.Models.HttpHandlers;

public class SystemToDeveloperRoleHandler(HttpMessageHandler innerHandler, ILoggerFactory output) : DelegatingHandler(innerHandler)
{
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new() { WriteIndented = true };
    private Regex _apiRegex = new(@"\d{4}-\d{2}-\d{2}(-preview)?$");
    private readonly ILoggerFactory _loggerFactory = output;
    private ILogger<LoggingHandler> _output => _loggerFactory.CreateLogger<LoggingHandler>();
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Only handle if there is content and it's JSON.
       
        if (request.Content is not { Headers.ContentType.MediaType: "application/json" })
            return await base.SendAsync(request, cancellationToken);
        var originalBody = await request.Content.ReadAsStringAsync(cancellationToken);
        if (request.RequestUri is not null)
        {
            // Find the API version in the request URI and replace it with the latest version.
            var match = _apiRegex.Match(request.RequestUri.ToString());
            var currentValue = match.Value;
            if (match.Success)
            {
                request.RequestUri = new Uri(request.RequestUri.ToString().Replace(currentValue, "2025-01-01-preview"));
            }
        }
        _output.LogInformation("=== Original Request ===");
        _output.LogInformation(originalBody);
        _output.LogInformation(string.Empty);
        if (string.IsNullOrWhiteSpace(originalBody)) return await base.SendAsync(request, cancellationToken);
        var isStreaming = false;
        try
        {
            var root = JsonNode.Parse(originalBody);
            if (root is not null)
            {
                isStreaming = root["stream"]?.GetValue<bool>() ?? false;
                var maxTokens = root["max_tokens"]?.GetValue<int>();
                root["max_completion_tokens"] = maxTokens;
                var keysToRemove = new[] { "top_p", "presence_penalty", "frequency_penalty", "logprobs", "top_logprobs", "logit_bias", "temperature", "max_tokens" };
                foreach (var key in keysToRemove)
                {
                    if (root is JsonObject obj)
                    {
                        obj.Remove(key);
                    }
                }
                var modelValue = root["model"]?.GetValue<string>() ?? string.Empty;
                var isO1Mini = modelValue.Contains("o1-mini");
                var messages = root["messages"]?.AsArray();
                if (messages is not null)
                {
                    foreach (var message in messages)
                    {
                        if (message?["role"]?.GetValue<string>() == "system")
                        {
                            message["role"] = isO1Mini ? "user" : "developer";
                        }
                    }

                    var modifiedJson = root.ToJsonString(new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    _output.LogInformation("=== Modified Request ===");
                    _output.LogInformation(modifiedJson);
                    _output.LogInformation(string.Empty);
                    request.Content = new StringContent(modifiedJson, Encoding.UTF8, "application/json");
                }
            }
        }
        catch
        {
            // If parsing fails, do nothing – just fall through
            // and send the original request body.
        }
        var response = await base.SendAsync(request, cancellationToken);
        if (isStreaming)
        {
            MemoryStream responseStream = new(await response.Content.ReadAsByteArrayAsync(cancellationToken));
            _output.LogInformation(await new StreamReader(responseStream).ReadToEndAsync(cancellationToken));
            responseStream.Position = 0;
            response.Content = new StreamContent(responseStream);
            return response;
        }
        
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _output.LogInformation("=== RESPONSE ===");
        _output.LogInformation(responseBody);
        _output.LogInformation(string.Empty);
        return response;
    }
}