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

        // A display-name form ("Ann" <ann@example.com>) keeps the address between its angle brackets: the first '<'
        // outside a quoted string, to the '>' that ends the value. A quoted local part may itself hold angle brackets
        // ("<jane@bigcorp.com>"@attacker.com), and reading those as the address would let a sender take on someone else's.
        var open = IndexOfUnquoted(value, '<');

        if (open >= 0 && value[^1] == '>')
        {
            value = value.Substring(open + 1, value.Length - open - 2).Trim();
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

        // A stray angle bracket left at either end of a typed address (<ann@example.com) is repaired. Quotes are never
        // removed: a sender can write "jane@bigcorp.com" or 'jane@bigcorp.com' as its whole address, which is not
        // jane@bigcorp.com to any mail system, and stripping the quotes would make it so here. IsValid refuses them.
        value = value.Trim().Trim('<', '>').Trim();

        return value.Length == 0
            ? null
            : value.ToLowerInvariant();
    }

    // The index of the first occurrence of a character outside a quoted string, honoring backslash escapes in quotes.
    private static int IndexOfUnquoted(string value, char character)
    {
        var quoted = false;

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];

            if (quoted && current == '\\')
            {
                i++;

                continue;
            }

            if (current == '"')
            {
                quoted = !quoted;

                continue;
            }

            if (!quoted && current == character)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Returns each address as it was given, trimmed, and in its canonical form: the values to look for where an address
    /// may be stored as it was typed (a contact's primary email) or in its canonical form (a destination, a message, the
    /// delivery log). The values are kept apart by exact spelling, because PostgreSQL and SQLite compare text by case,
    /// so a canonical form that differs from the given one only in case is a value of its own.
    /// </summary>
    /// <param name="addresses">The addresses, in any form <see cref="Normalize"/> accepts.</param>
    /// <returns>The distinct spellings; empty when no address was given.</returns>
    public static string[] WithCanonicalForms(IEnumerable<string> addresses)
        => (addresses ?? [])
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .SelectMany(address => new[] { address.Trim(), Normalize(address) })
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Determines whether a value is shaped like an email address: one <c>@</c> with a local part before it and a
    /// dotted host name of letters, digits and hyphens after it, and no whitespace, double quotes or angle brackets. A
    /// quoted local part is legal but unused by real mailboxes, and it is how a sender would dress one address up as
    /// another; so is a single quote around the address, which leaves a domain with no DMARC policy of its own. The
    /// provider is the authority on whether the mailbox exists.
    /// </summary>
    /// <param name="address">The address to check, in any form <see cref="Normalize"/> accepts.</param>
    /// <returns><see langword="true"/> when the address is usable.</returns>
    public static bool IsValid(string address)
    {
        var normalized = Normalize(address);

        if (string.IsNullOrEmpty(normalized) ||
            normalized.Length > 254 ||
            normalized.Any(character => char.IsWhiteSpace(character) || character is '<' or '>' or '"'))
        {
            return false;
        }

        var at = normalized.IndexOf('@');

        if (at <= 0 || at != normalized.LastIndexOf('@') || at == normalized.Length - 1)
        {
            return false;
        }

        var localPart = normalized.Substring(0, at);
        var domain = normalized.Substring(at + 1);

        // An apostrophe is legal inside a local part (o'brien@example.com), but not around it.
        if (localPart[0] == '\'' || localPart[^1] == '\'')
        {
            return false;
        }

        return domain.All(character => char.IsLetterOrDigit(character) || character is '.' or '-') &&
            domain.Contains('.') &&
            !domain.StartsWith('.') &&
            !domain.EndsWith('.') &&
            !domain.Contains("..", StringComparison.Ordinal);
    }
}
