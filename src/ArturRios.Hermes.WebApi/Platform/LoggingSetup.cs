using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace ArturRios.Hermes.WebApi.Platform;

/// <summary>
/// Structured JSON to the console and nowhere else: the container has no writable disk, and the platform collects
/// container logs. Request details are logged by Serilog's request logging, which records no headers, so the
/// <c>Authorization</c> header and the API key it carries never reach a log line.
/// </summary>
public static class LoggingSetup
{
    public static LoggerConfiguration Configure(LoggerConfiguration configuration) => configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(new JsonFormatter());
}
