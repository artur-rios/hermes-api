using System.Globalization;
using ArturRios.Hermes.WebApi.Emails;

namespace ArturRios.Hermes.WebApi.Clients;

/// <summary>
/// Reads the clients from the environment at startup and checks them, together with what production requires. Every
/// problem is collected, so one deploy shows them all. A problem names the client and the setting and never repeats a
/// key hash.
/// </summary>
public static class ClientConfiguration
{
    public const string ClientsSection = "HERMES_CLIENTS";
    public const string AlertAddressVariable = "HERMES_ALERT_TO";

    private const string FixedPolicy = "fixed";
    private const string AnyPolicy = "any";
    private const int MaximumKeys = 2;
    private const int Sha256HexLength = 64;

    /// <exception cref="InvalidConfigurationException">The configuration cannot be used.</exception>
    public static ClientDirectory Load(IConfiguration configuration, bool isProduction)
    {
        var problems = new List<string>();
        var mailgun = MailgunSettings.From(configuration);

        CheckEnvironment(configuration, mailgun, isProduction, problems);

        var clients = configuration.GetSection(ClientsSection).GetChildren()
            .Select((section, position) => ReadClient(section, position, mailgun, problems))
            .OfType<Client>()
            .ToList();

        CheckUnique(clients, problems);

        return problems.Count > 0 ? throw new InvalidConfigurationException(problems) : new ClientDirectory(clients);
    }

    private static void CheckEnvironment(
        IConfiguration configuration, MailgunSettings mailgun, bool isProduction, List<string> problems)
    {
        var alertAddress = configuration[AlertAddressVariable];

        if (isProduction && !mailgun.IsConfigured)
        {
            foreach (var variable in new[] { MailgunSettings.ApiKeyVariable, MailgunSettings.DomainVariable })
            {
                if (string.IsNullOrWhiteSpace(configuration[variable]))
                {
                    problems.Add($"{variable} is required in production.");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(alertAddress))
        {
            if (isProduction)
            {
                problems.Add($"{AlertAddressVariable} is required in production.");
            }
        }
        else if (!Mailbox.IsAddress(alertAddress))
        {
            problems.Add($"{AlertAddressVariable} is not one valid e-mail address.");
        }
    }

    private static Client? ReadClient(IConfigurationSection section, int position, MailgunSettings mailgun, List<string> problems)
    {
        var id = section["ID"];
        var name = string.IsNullOrWhiteSpace(id) ? $"#{position}" : id;
        var countBefore = problems.Count;

        void Problem(string text) => problems.Add($"Client '{name}': {text}");

        if (string.IsNullOrWhiteSpace(id))
        {
            Problem("ID is required.");
        }

        var keyHashes = ReadKeyHashes(section, Problem);
        var sender = ReadSender(section, mailgun, Problem);
        var policy = ReadPolicy(section, Problem);
        var limitPerMinute = ReadLimit(section, "LIMITPERMINUTE", Problem);
        var limitPerDay = ReadLimit(section, "LIMITPERDAY", Problem);

        return problems.Count > countBefore
            ? null
            : new Client(id!, keyHashes, sender!, policy!, limitPerMinute, limitPerDay);
    }

    private static List<string> Values(IConfigurationSection section, string setting) =>
        section.GetSection(setting).GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();

    private static List<string> ReadKeyHashes(IConfigurationSection section, Action<string> problem)
    {
        var hashes = Values(section, "KEYHASHES");

        if (hashes.Count is 0 or > MaximumKeys)
        {
            problem($"KEYHASHES must hold one or two keys, and holds {hashes.Count}.");
        }

        if (hashes.Any(hash => hash.Length != Sha256HexLength || !hash.All(Uri.IsHexDigit)))
        {
            problem("KEYHASHES holds a value that is not a hexadecimal SHA-256 hash.");
        }

        return hashes.Select(hash => hash.ToLowerInvariant()).ToList();
    }

    private static string? ReadSender(IConfigurationSection section, MailgunSettings mailgun, Action<string> problem)
    {
        var sender = section["SENDER"];

        if (string.IsNullOrWhiteSpace(sender))
        {
            problem("SENDER is required.");

            return null;
        }

        var address = Mailbox.AddressOfSender(sender);

        if (address is null)
        {
            problem("SENDER is not one e-mail address, optionally with a display name.");

            return null;
        }

        if (mailgun.IsConfigured && !string.Equals(Mailbox.DomainOf(address), mailgun.Domain, StringComparison.OrdinalIgnoreCase))
        {
            problem($"SENDER is not on the Mailgun domain {mailgun.Domain}.");
        }

        return sender;
    }

    private static RecipientPolicy? ReadPolicy(IConfigurationSection section, Action<string> problem)
    {
        switch (section["RECIPIENTPOLICY"])
        {
            case AnyPolicy:
                return RecipientPolicy.Any;

            case FixedPolicy:
                var recipients = Values(section, "ALLOWEDRECIPIENTS");

                if (recipients.Count == 0)
                {
                    problem($"ALLOWEDRECIPIENTS must hold at least one address under a {FixedPolicy} policy.");
                }

                if (!recipients.All(Mailbox.IsAddress))
                {
                    problem("ALLOWEDRECIPIENTS holds a value that is not one valid e-mail address.");
                }

                return RecipientPolicy.Fixed(recipients);

            case null or "":
                problem("RECIPIENTPOLICY is required.");

                return null;

            default:
                problem($"RECIPIENTPOLICY must be {FixedPolicy} or {AnyPolicy}.");

                return null;
        }
    }

    private static int ReadLimit(IConfigurationSection section, string setting, Action<string> problem)
    {
        var value = section[setting];

        if (string.IsNullOrWhiteSpace(value))
        {
            problem($"{setting} is required.");

            return 0;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var limit) || limit <= 0)
        {
            problem($"{setting} must be a whole number greater than zero.");

            return 0;
        }

        return limit;
    }

    private static void CheckUnique(List<Client> clients, List<string> problems)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hashOwners = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var client in clients)
        {
            if (!ids.Add(client.Id))
            {
                problems.Add($"Client '{client.Id}': ID is used by more than one client.");
            }

            foreach (var hash in client.KeyHashes)
            {
                if (!hashOwners.TryAdd(hash, client.Id))
                {
                    problems.Add($"Client '{client.Id}': KEYHASHES holds a key already used by client '{hashOwners[hash]}'.");
                }
            }
        }
    }
}
