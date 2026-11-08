using Bogus;
using Microsoft.Extensions.Configuration;

namespace ArturRios.Hermes.WebApi.Tests;

/// <summary>
/// The environment configuration of a set of clients, as the indexed variables reach the configuration system.
/// </summary>
internal sealed class ClientSettings
{
    private static readonly Faker Faker = new();

    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);

    public static string KeyHash() => Faker.Random.Hexadecimal(64, string.Empty).ToLowerInvariant();

    /// <summary>A client with every required setting, under a <c>fixed</c> policy.</summary>
    public ClientSettings Client(int index, string id, string? keyHash = null, string sender = "Client <client@mg.example.com>")
    {
        Set(index, "ID", id);
        Set(index, "KEYHASHES:0", keyHash ?? KeyHash());
        Set(index, "SENDER", sender);
        Set(index, "RECIPIENTPOLICY", "fixed");
        Set(index, "ALLOWEDRECIPIENTS:0", "owner@example.com");
        Set(index, "LIMITPERMINUTE", "5");
        Set(index, "LIMITPERDAY", "50");

        return this;
    }

    public ClientSettings Set(int index, string setting, string? value)
    {
        _values[$"HERMES_CLIENTS:{index}:{setting}"] = value;

        return this;
    }

    public ClientSettings Remove(int index, string setting)
    {
        _values.Remove($"HERMES_CLIENTS:{index}:{setting}");

        return this;
    }

    public ClientSettings Global(string key, string? value)
    {
        _values[key] = value;

        return this;
    }

    public ClientSettings WithMailgun(string domain = "mg.example.com") =>
        Global("MAILGUN_API_KEY", "key").Global("MAILGUN_DOMAIN", domain);

    public ClientSettings ReadyForProduction() => WithMailgun().Global("HERMES_ALERT_TO", "owner@example.com");

    public IReadOnlyDictionary<string, string?> Values => _values;

    public IConfiguration Build() => new ConfigurationBuilder().AddInMemoryCollection(_values).Build();
}
