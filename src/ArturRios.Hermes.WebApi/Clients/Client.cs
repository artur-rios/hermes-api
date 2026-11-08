namespace ArturRios.Hermes.WebApi.Clients;

/// <summary>A project allowed to call Hermes, as the environment configures it.</summary>
/// <param name="Id">Short name, used in logs and alerts and never accepted from a caller.</param>
/// <param name="KeyHashes">Lowercase hexadecimal SHA-256 hashes of the one or two active keys.</param>
/// <param name="Sender">The sender of every message, with an optional display name.</param>
/// <param name="Policy">Who the client may send to.</param>
/// <param name="LimitPerMinute">Messages per fixed minute.</param>
/// <param name="LimitPerDay">Messages per UTC day.</param>
public sealed record Client(
    string Id,
    IReadOnlyList<string> KeyHashes,
    string Sender,
    RecipientPolicy Policy,
    int LimitPerMinute,
    int LimitPerDay);
