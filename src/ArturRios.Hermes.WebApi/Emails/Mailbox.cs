using System.Net.Mail;

namespace ArturRios.Hermes.WebApi.Emails;

/// <summary>
/// What counts as one e-mail address. <see cref="MailAddress"/> alone is not enough: it reads
/// <c>a@x.com, b@x.com</c> as the address <c>b@x.com</c> with a display name, so the parsed address must account for
/// the whole value.
/// </summary>
public static class Mailbox
{
    /// <summary>Whether <paramref name="value"/> is exactly one address, with no display name.</summary>
    public static bool IsAddress(string? value) =>
        MailAddress.TryCreate(value, out var mailbox) && string.Equals(value, mailbox.Address, StringComparison.OrdinalIgnoreCase);

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

    /// <summary>The domain of an address already known to be valid.</summary>
    public static string DomainOf(string address) => address[(address.LastIndexOf('@') + 1)..];
}
