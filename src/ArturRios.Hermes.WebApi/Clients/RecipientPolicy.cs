namespace ArturRios.Hermes.WebApi.Clients;

/// <summary>Who a client may send to: a fixed list of addresses, or anyone.</summary>
public sealed class RecipientPolicy
{
    private readonly HashSet<string>? _allowed;

    private RecipientPolicy(HashSet<string>? allowed) => _allowed = allowed;

    public static RecipientPolicy Any { get; } = new(null);

    public static RecipientPolicy Fixed(IEnumerable<string> addresses) =>
        new(new HashSet<string>(addresses, StringComparer.OrdinalIgnoreCase));

    public bool Permits(string recipient) => _allowed is null || _allowed.Contains(recipient);
}
