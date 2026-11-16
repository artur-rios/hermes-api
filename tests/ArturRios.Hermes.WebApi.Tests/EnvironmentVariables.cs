namespace ArturRios.Hermes.WebApi.Tests;

/// <summary>
/// Sets process environment variables for as long as it lives, and puts back what each held before, set or not, so
/// a test leaves the environment as it found it. Tests using it belong to <see cref="ProcessStateCollection"/>.
/// </summary>
internal sealed class EnvironmentVariables : IDisposable
{
    private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);

    public EnvironmentVariables(params (string Name, string? Value)[] variables)
    {
        foreach (var (name, value) in variables)
        {
            _previous.TryAdd(name, Environment.GetEnvironmentVariable(name));
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    /// <summary>The Mailgun credentials ArturRios.Messaging reads from the environment when it sends.</summary>
    public static EnvironmentVariables WithMailgun(string domain = "mg.example.com") =>
        new(("MAILGUN_API_KEY", "key"), ("MAILGUN_DOMAIN", domain));

    public void Dispose()
    {
        foreach (var (name, value) in _previous)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
