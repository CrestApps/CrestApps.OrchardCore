namespace CrestApps.OrchardCore.Omnichannel.Core;

/// <summary>
/// The one canonical form of an email address everywhere Omnichannel stores or matches one: the business's
/// addresses, a contact's addresses, an activity's destination and the conversation key of the email channel. Two
/// spellings of the same mailbox must meet, or one customer's emails fork into several threads and a reply never finds
/// the activity it answers.
/// </summary>
public static class OmnichannelEmailAddress
{
    private const string MailtoPrefix = "mailto:";

    /// <summary>
    /// Brings an email address into its canonical form: the bare address, without a display name, a <c>mailto:</c>
    /// prefix or surrounding angle brackets, trimmed and in lower case.
    /// </summary>
    /// <remarks>
    /// The local part of an address is case-sensitive by the letter of RFC 5321, but no mail system in use treats it
    /// so, and every contact center and help desk matches addresses without regard to case. Sub-addresses
    /// (<c>name+tag@</c>) are kept, because they are how many people route their own mail.
    /// </remarks>
    /// <param name="address">The address as it was received or typed, for example <c>"Ann Lee" &lt;Ann@Example.com&gt;</c>.</param>
    /// <returns>The canonical address, or <see langword="null"/> when <paramref name="address"/> is empty.</returns>
    public static string Normalize(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var value = address.Trim();

        // A display-name form keeps the address between the last pair of angle brackets.
        var open = value.LastIndexOf('<');
        var close = value.LastIndexOf('>');

        if (open >= 0 && close > open)
        {
            value = value.Substring(open + 1, close - open - 1).Trim();
        }

        if (value.StartsWith(MailtoPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(MailtoPrefix.Length);

            // A mailto link can carry a query (?subject=...), which is not part of the address.
            var query = value.IndexOf('?');

            if (query >= 0)
            {
                value = value.Substring(0, query);
            }
        }

        value = value.Trim().Trim('<', '>', '"', '\'').Trim();

        return value.Length == 0
            ? null
            : value.ToLowerInvariant();
    }

    /// <summary>
    /// Determines whether a value is shaped like an email address: one <c>@</c> with a local part before it and a
    /// dotted domain after it, and no whitespace. The provider is the authority on whether the mailbox exists.
    /// </summary>
    /// <param name="address">The address to check, in any form <see cref="Normalize"/> accepts.</param>
    /// <returns><see langword="true"/> when the address is usable.</returns>
    public static bool IsValid(string address)
    {
        var normalized = Normalize(address);

        if (string.IsNullOrEmpty(normalized) || normalized.Length > 254 || normalized.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var at = normalized.IndexOf('@');

        if (at <= 0 || at != normalized.LastIndexOf('@') || at == normalized.Length - 1)
        {
            return false;
        }

        var domain = normalized.Substring(at + 1);

        return domain.Contains('.') &&
            !domain.StartsWith('.') &&
            !domain.EndsWith('.') &&
            !domain.Contains("..", StringComparison.Ordinal);
    }
}
