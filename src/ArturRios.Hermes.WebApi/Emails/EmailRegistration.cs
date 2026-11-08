using ArturRios.Hermes.WebApi.Clients;
using ArturRios.Hermes.WebApi.Limits;
using ArturRios.Messaging.Email;

namespace ArturRios.Hermes.WebApi.Emails;

public static class EmailRegistration
{
    public static readonly TimeSpan MailgunTimeout = TimeSpan.FromSeconds(10);

    public static void Add(IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ClientAuthenticator>();
        services.AddSingleton<UsageCounters>();
        services.AddSingleton<SendEmailRequestValidator>();
        services.AddSingleton<LoggingEmailService>();
        services.AddSingleton(provider => MailgunSettings.From(provider.GetRequiredService<IConfiguration>()));
        services.AddHttpClient<MailgunEmailService>(client => client.Timeout = MailgunTimeout);

        // Decided on each resolve from the configuration, which production has already checked at startup.
        services.AddTransient<IEmailService>(provider => provider.GetRequiredService<MailgunSettings>().IsConfigured
            ? provider.GetRequiredService<MailgunEmailService>()
            : provider.GetRequiredService<LoggingEmailService>());

        services.AddTransient<SendEmailHandler>();
    }
}
