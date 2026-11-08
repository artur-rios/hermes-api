using ArturRios.Hermes.WebApi.Platform;
using Serilog;

namespace ArturRios.Hermes.WebApi;

public class Program
{
    public static void Main(string[] args) => CreateApplication(CreateBuilder(args)).Run();

    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((_, configuration) => LoggingSetup.Configure(configuration));

        return builder;
    }

    public static WebApplication CreateApplication(WebApplicationBuilder builder)
    {
        var app = builder.Build();

        app.UseSerilogRequestLogging();

        HealthEndpoint.Map(app);

        return app;
    }
}
