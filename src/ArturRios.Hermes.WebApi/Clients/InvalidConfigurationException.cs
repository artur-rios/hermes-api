namespace ArturRios.Hermes.WebApi.Clients;

/// <summary>The environment's configuration cannot be used, so Hermes must not start.</summary>
public sealed class InvalidConfigurationException(IReadOnlyList<string> problems)
    : Exception(string.Join(Environment.NewLine, problems))
{
    public IReadOnlyList<string> Problems { get; } = problems;
}
