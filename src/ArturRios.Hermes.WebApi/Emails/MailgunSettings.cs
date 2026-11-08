namespace ArturRios.Hermes.WebApi.Emails;

/// <summary>Whether this environment holds the Mailgun credentials, read once from the configuration.</summary>
public sealed class MailgunSettings
{
    public const string ApiKeyVariable = "MAILGUN_API_KEY";
    public const string DomainVariable = "MAILGUN_DOMAIN";

    private MailgunSettings(bool isConfigured, string? domain)
    {
        IsConfigured = isConfigured;
        Domain = domain;
    }

    /// <summary>Both the key and the domain are set. One without the other cannot deliver anything.</summary>
    public bool IsConfigured { get; }

    public string? Domain { get; }

    public static MailgunSettings From(IConfiguration configuration)
    {
        var apiKey = configuration[ApiKeyVariable];
        var domain = configuration[DomainVariable];

        return new MailgunSettings(!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(domain), domain);
    }
}
