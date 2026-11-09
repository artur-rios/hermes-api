using ArturRios.Messaging.Email;
using ArturRios.Output;

namespace ArturRios.Hermes.WebApi.Emails;

/// <summary>Stands in for Mailgun outside production when it is not configured. Nothing is sent or kept.</summary>
public sealed class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    public Task<ProcessOutput> SendEmailAsync(string to, string subject, string body) => Log(to);

    public Task<ProcessOutput> SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default) => Log(message.To);

    private Task<ProcessOutput> Log(string to)
    {
        logger.LogInformation("Mailgun is not configured: a message to {RecipientRef} would have been sent", Mailbox.Reference(to));

        return Task.FromResult(new ProcessOutput());
    }
}
