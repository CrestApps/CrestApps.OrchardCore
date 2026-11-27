using CrestApps.OrchardCore.Omnichannel.Core;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

/// <summary>
/// Reads whether an inbound email's <c>From</c> domain was authenticated (DMARC, or a DKIM signature aligned with it) from
/// what the system that received the email reported. Only a report the receiving side wrote is worth anything: a sender
/// can put any header it likes into its own email.
/// </summary>
public static class InboundEmailAuthentication
{
    /// <summary>
    /// The header a receiving mail system stamps with its authentication results (RFC 8601).
    /// </summary>
    public const string AuthenticationResultsHeader = "Authentication-Results";

    /// <summary>
    /// Determines whether an <c>Authentication-Results</c> header reports a DMARC pass for the domain of
    /// <paramref name="fromAddress"/>, for example
    /// <c>mx.example.net; spf=pass smtp.mailfrom=contoso.com; dmarc=pass action=none header.from=contoso.com</c>.
    /// </summary>
    /// <remarks>
    /// Read it only from a header the receiving system added (the topmost one, from a mailbox host that always adds it),
    /// never from one a webhook passes on unchecked, since the sender may have written that one.
    /// </remarks>
    /// <param name="authenticationResults">The header's value.</param>
    /// <param name="fromAddress">The email's <c>From</c> address.</param>
    /// <returns><see langword="true"/> when DMARC passed for the <c>From</c> domain.</returns>
    public static bool HasDmarcPass(string authenticationResults, string fromAddress)
    {
        var domain = GetDomain(fromAddress);

        if (string.IsNullOrWhiteSpace(authenticationResults) || domain is null)
        {
            return false;
        }

        // The first part is the receiving system's own name; each part after it is one method's result with its
        // properties, such as "dmarc=pass (p=none) header.from=contoso.com".
        foreach (var part in authenticationResults.Split(';').Skip(1))
        {
            var tokens = RemoveComments(part).Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0 || !string.Equals(tokens[0], "dmarc=pass", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var headerFrom = tokens
                .Where(token => token.StartsWith("header.from=", StringComparison.OrdinalIgnoreCase))
                .Select(token => token["header.from=".Length..].Trim('"', '\'', '<', '>'))
                .FirstOrDefault();

            if (string.Equals(headerFrom, domain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether SendGrid's Inbound Parse <c>dkim</c> field, such as <c>{@contoso.com : pass}</c>, reports a
    /// passing DKIM signature by the domain of <paramref name="fromAddress"/>: a signature aligned with the <c>From</c>
    /// domain, which is what DMARC asks for.
    /// </summary>
    /// <param name="sendGridDkim">The field's value.</param>
    /// <param name="fromAddress">The email's <c>From</c> address.</param>
    /// <returns><see langword="true"/> when a signature by the <c>From</c> domain passed.</returns>
    public static bool HasAlignedDkimPass(string sendGridDkim, string fromAddress)
    {
        var domain = GetDomain(fromAddress);

        if (string.IsNullOrWhiteSpace(sendGridDkim) || domain is null)
        {
            return false;
        }

        foreach (var entry in sendGridDkim.Trim().Trim('{', '}').Split([',', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = entry.LastIndexOf(':');

            if (separator <= 0)
            {
                continue;
            }

            var signer = entry[..separator].Trim().TrimStart('@');
            var result = entry[(separator + 1)..].Trim();

            if (string.Equals(signer, domain, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(result, "pass", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetDomain(string address)
    {
        var normalized = OmnichannelEmailAddress.Normalize(address);
        var at = normalized?.LastIndexOf('@') ?? -1;

        return at > 0 && at < normalized.Length - 1 ? normalized[(at + 1)..] : null;
    }

    // Comments in parentheses, such as "(signature was verified)", can hold any text, including "header.from=".
    private static string RemoveComments(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        var depth = 0;

        foreach (var character in value)
        {
            if (character == '(')
            {
                depth++;
            }
            else if (character == ')' && depth > 0)
            {
                depth--;
            }
            else if (depth == 0)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
