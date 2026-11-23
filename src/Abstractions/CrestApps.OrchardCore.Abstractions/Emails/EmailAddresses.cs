using System.Net.Mail;

namespace CrestApps.OrchardCore.Emails;

/// <summary>
/// The one rule for comparing, checking and de-duplicating e-mail addresses, so every module matches an address the
/// same way (consent, coupons, mailing lists, contacts).
/// </summary>
public static class EmailAddresses
{
    /// <summary>
    /// The longest address a mail server accepts (RFC 5321 path limit).
    /// </summary>
    public const int MaxLength = 254;

    private static readonly string[] _gmailDomains = ["gmail.com", "googlemail.com"];

    /// <summary>
    /// Returns the address trimmed and in lower case, the form addresses are stored, indexed and compared in, or
    /// <see langword="null"/> when the value is empty.
    /// </summary>
    /// <param name="address">The address as the person typed it.</param>
    public static string Normalize(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        return address.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Returns whether the value is one plain e-mail address (no display name, no list), with a domain that has a dot,
    /// no longer than <see cref="MaxLength"/>.
    /// </summary>
    /// <param name="address">The address to check.</param>
    public static bool IsValid(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var trimmed = address.Trim();

        if (trimmed.Length > MaxLength || trimmed.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (!MailAddress.TryCreate(trimmed, out var parsed) || !string.Equals(parsed.Address, trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        var host = parsed.Host;

        return !string.IsNullOrEmpty(parsed.User)
            && host.Contains('.', StringComparison.Ordinal)
            && !host.StartsWith('.')
            && !host.EndsWith('.');
    }

    /// <summary>
    /// Returns a key that is the same for the aliases of one mailbox: the normalized address with a <c>+tag</c> removed
    /// from the local part, and, for Gmail, the dots removed and <c>googlemail.com</c> read as <c>gmail.com</c>. Use it
    /// only to notice that one person signed up again (for example to give a welcome offer once); never send to it.
    /// </summary>
    /// <param name="address">The address as the person typed it.</param>
    public static string AbuseKey(string address)
    {
        var normalized = Normalize(address);

        if (normalized is null)
        {
            return null;
        }

        var at = normalized.LastIndexOf('@');

        if (at <= 0 || at == normalized.Length - 1)
        {
            return normalized;
        }

        var local = normalized[..at];
        var domain = normalized[(at + 1)..];
        var plus = local.IndexOf('+', StringComparison.Ordinal);

        if (plus > 0)
        {
            local = local[..plus];
        }

        if (_gmailDomains.Contains(domain, StringComparer.Ordinal))
        {
            local = local.Replace(".", string.Empty, StringComparison.Ordinal);
            domain = _gmailDomains[0];
        }

        return $"{local}@{domain}";
    }
}
