using ArturRios.Hermes.WebApi.Clients;
using ArturRios.Hermes.WebApi.Emails;
using ArturRios.Hermes.WebApi.Platform;
using Serilog;

namespace ArturRios.Hermes.WebApi;

public class Program
{
    public static int Main(string[] args)
    {
        try
        {
            CreateApplication(CreateBuilder(args)).Run();

            return 0;
        }
        catch (InvalidConfigurationException exception)
        {
            using var logger = LoggingSetup.Configure(new LoggerConfiguration()).CreateLogger();

            logger.Fatal("Hermes refused to start: {Problems}", exception.Problems);

            return 1;
        }
    }

    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((_, configuration) => LoggingSetup.Configure(configuration));

        builder.Services.AddSingleton(services => ClientConfiguration.Load(
            services.GetRequiredService<IConfiguration>(),
            services.GetRequiredService<IHostEnvironment>().IsProduction()));

        EmailRegistration.Add(builder.Services);

        MetricsSetup.Add(builder.Services);

        return builder;
    }

    /// <exception cref="InvalidConfigurationException">The configuration cannot be used, so nothing may start.</exception>
    public static WebApplication CreateApplication(WebApplicationBuilder builder)
    {
        var app = builder.Build();

        app.Services.GetRequiredService<ClientDirectory>();

        MetricsSetup.Use(app);

        app.UseSerilogRequestLogging();

        HealthEndpoint.Map(app);

        SendEmailEndpoint.Map(app);

        return app;
    }
}
