using Microsoft.Extensions.Logging;

namespace SirChartsALot.Core.Models.HttpHandlers;

public class DelegateHandlerFactory
{

    public static DelegatingHandler GetDelegatingHandler<T>(ILoggerFactory output)
    {
        if (typeof(T) == typeof(LoggingHandler))
            return new LoggingHandler(new HttpClientHandler(), output);
        if (typeof(T) == typeof(SystemToDeveloperRoleHandler))
            return new SystemToDeveloperRoleHandler(new HttpClientHandler(), output);
        throw new NotSupportedException($"The type {typeof(T).Name} is not supported.");
    }
    public static HttpClient GetHttpClient<T>(ILoggerFactory output)
    {
        return new HttpClient(GetDelegatingHandler<T>(output)) { Timeout = TimeSpan.FromMinutes(5) };
    }
}