namespace ArturRios.Hermes.WebApi.Clients;

/// <summary>The clients of this environment, fixed at startup: a change takes a redeploy.</summary>
public sealed class ClientDirectory(IReadOnlyList<Client> clients)
{
    public IReadOnlyList<Client> Clients { get; } = clients;
}
