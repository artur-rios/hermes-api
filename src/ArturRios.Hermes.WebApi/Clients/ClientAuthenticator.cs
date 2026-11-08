using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace ArturRios.Hermes.WebApi.Clients;

/// <summary>
/// Identifies the calling client from its key alone. The presented key is hashed and compared with every active hash
/// in constant time, with no early exit, so the time taken says nothing about which hash came close.
/// </summary>
public sealed class ClientAuthenticator
{
    private const string BearerScheme = "Bearer";

    private readonly (byte[] Hash, Client Client)[] _activeKeys;

    public ClientAuthenticator(ClientDirectory directory) =>
        _activeKeys = directory.Clients
            .SelectMany(client => client.KeyHashes.Select(hash => (Convert.FromHexString(hash), client)))
            .ToArray();

    /// <returns>The client the key belongs to, or <see langword="null"/> when it is missing or matches no active key.</returns>
    public Client? Authenticate(string? authorizationHeader)
    {
        if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var header)
            || !string.Equals(header.Scheme, BearerScheme, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(header.Parameter))
        {
            return null;
        }

        var presented = SHA256.HashData(Encoding.UTF8.GetBytes(header.Parameter));

        Client? match = null;

        foreach (var (hash, client) in _activeKeys)
        {
            if (CryptographicOperations.FixedTimeEquals(hash, presented))
            {
                match = client;
            }
        }

        return match;
    }
}
