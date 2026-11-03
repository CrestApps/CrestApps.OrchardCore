using System.Text;

namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Carries the custom SIP headers an inbound call arrived with to the AI conversation that answers it.
/// </summary>
/// <remarks>
/// A PBX or a carrier forwarding a call can add <c>X-</c> headers to its INVITE, such as the caller's account number or
/// the menu option they chose before the transfer. A provider puts them on the inbound event's metadata with
/// <see cref="AddToMetadata"/>, and the AI entry point turns them into conversation variables with
/// <see cref="ToVariables"/>: <c>X-Account-Id: 42</c> becomes <c>sip_account_id</c> = <c>42</c>. Only <c>X-</c>
/// headers are carried, at most <see cref="MaxHeaders"/> of them, each value cut to <see cref="MaxValueLength"/>
/// characters: the standard headers describe the call's routing, not the customer, and a caller's equipment must not be
/// able to fill a conversation with data.
/// </remarks>
public static class SipHeaderVariables
{
    /// <summary>
    /// The prefix of the metadata keys that carry a header, followed by the header's name.
    /// </summary>
    public const string MetadataPrefix = "sip_header:";

    /// <summary>
    /// The prefix of the conversation variables made from the headers.
    /// </summary>
    public const string VariablePrefix = "sip_";

    /// <summary>
    /// The most headers carried from one call.
    /// </summary>
    public const int MaxHeaders = 20;

    /// <summary>
    /// The longest header value carried; longer values are cut.
    /// </summary>
    public const int MaxValueLength = 256;

    /// <summary>
    /// Adds an inbound call's custom headers to its event's metadata.
    /// </summary>
    /// <param name="metadata">The inbound event's metadata.</param>
    /// <param name="headers">The headers the call arrived with, as name and value.</param>
    /// <returns>The headers added.</returns>
    public static int AddToMetadata(IDictionary<string, string> metadata, IEnumerable<KeyValuePair<string, string>> headers)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (headers is null)
        {
            return 0;
        }

        var added = 0;

        foreach (var (name, value) in headers)
        {
            if (added >= MaxHeaders)
            {
                break;
            }

            if (ToVariableName(name) is null || Clean(value) is not { Length: > 0 } cleaned)
            {
                continue;
            }

            metadata[MetadataPrefix + name.Trim()] = cleaned;
            added++;
        }

        return added;
    }

    /// <summary>
    /// Reads the headers in an inbound event's metadata as conversation variables.
    /// </summary>
    /// <param name="metadata">The inbound event's metadata.</param>
    /// <returns>The variables, keyed by name; empty when the call carried no custom headers.</returns>
    public static IReadOnlyDictionary<string, string> ToVariables(IEnumerable<KeyValuePair<string, string>> metadata)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (metadata is null)
        {
            return variables;
        }

        foreach (var (key, value) in metadata)
        {
            if (variables.Count >= MaxHeaders)
            {
                break;
            }

            if (key is null ||
                !key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase) ||
                ToVariableName(key[MetadataPrefix.Length..]) is not { } name ||
                Clean(value) is not { Length: > 0 } cleaned)
            {
                continue;
            }

            variables[name] = cleaned;
        }

        return variables;
    }

    /// <summary>
    /// Gets the conversation variable a custom header becomes.
    /// </summary>
    /// <param name="headerName">The header's name, such as <c>X-Account-Id</c>.</param>
    /// <returns>The variable's name, such as <c>sip_account_id</c>, or <see langword="null"/> when the header is not a
    /// custom <c>X-</c> header with a usable name.</returns>
    public static string ToVariableName(string headerName)
    {
        var name = headerName?.Trim();

        if (string.IsNullOrEmpty(name) ||
            name.Length <= 2 ||
            !name.StartsWith("X-", StringComparison.OrdinalIgnoreCase) ||
            name.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            return null;
        }

        var builder = new StringBuilder(VariablePrefix);
        var pendingSeparator = false;

        foreach (var character in name[2..])
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > VariablePrefix.Length)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        return builder.Length > VariablePrefix.Length ? builder.ToString() : null;
    }

    private static string Clean(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = new string(value.Where(character => !char.IsControl(character)).ToArray()).Trim();

        return cleaned.Length > MaxValueLength ? cleaned[..MaxValueLength] : cleaned;
    }
}
