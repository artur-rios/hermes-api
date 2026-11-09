using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace ArturRios.Hermes.WebApi.Emails;

/// <summary>
/// What counts as one e-mail address. <see cref="MailAddress"/> alone is not enough: it reads
/// <c>a@x.com, b@x.com</c> as the address <c>b@x.com</c> with a display name, so the parsed address must account for
/// the whole value.
/// </summary>
public static class Mailbox
{
    /// <summary>
    /// Whether <paramref name="value"/> is exactly one deliverable address, with no display name. A quoted local part
    /// is refused because it can hold a comma, which a provider might read as a second recipient.
    /// </summary>
    public static bool IsAddress(string? value) =>
        MailAddress.TryCreate(value, out var mailbox)
        && string.Equals(value, mailbox.Address, StringComparison.OrdinalIgnoreCase)
        && !value.Contains('"')
        && HasDeliverableDomain(mailbox.Host);

    private static bool HasDeliverableDomain(string domain) =>
        domain.Contains('.') && !domain.EndsWith('.') && !domain.StartsWith('[');

    /// <summary>
    /// The address of a sender, which may carry a display name (<c>Example &lt;no-reply@example.com&gt;</c>), or
    /// <see langword="null"/> when <paramref name="value"/> is not exactly one mailbox.
    /// </summary>
    public static string? AddressOfSender(string? value)
    {
        if (!MailAddress.TryCreate(value, out var mailbox))
        {
            return null;
        }

        if (string.Equals(value, mailbox.Address, StringComparison.OrdinalIgnoreCase))
        {
            return mailbox.Address;
        }

        var bracketed = $"<{mailbox.Address}>";

        if (!value.EndsWith(bracketed, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var displayName = value[..^bracketed.Length].TrimEnd();

        var isQuoted = displayName.Length >= 2 && displayName[0] == '"' && displayName[^1] == '"';
        var isPlain = displayName.Length > 0 && displayName.IndexOfAny([',', ';', '@', '<', '>', '"']) < 0;

        return isQuoted || isPlain ? mailbox.Address : null;
    }

    /// <summary>
    /// How logs refer to a recipient: the first twelve hexadecimal characters of the SHA-256 of the trimmed, lower-cased
    /// address, the same reference ArturRios.Messaging logs.
    /// </summary>
    public static string Reference(string address) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(address.Trim().ToLowerInvariant())))[..12];

    /// <summary>
    /// The domain of an address already known to be valid and not an address literal. It is read from the last
    /// <c>@</c> because a sender's quoted local part can hold one.
    /// </summary>
    public static string DomainOf(string address) => address[(address.LastIndexOf('@') + 1)..];
}
