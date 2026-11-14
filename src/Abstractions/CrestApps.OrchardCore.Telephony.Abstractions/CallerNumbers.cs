namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Keeps a caller id to what a dialable number can contain. A caller id is chosen by whoever places the call, so it is
/// cleaned where voice events are mapped and before it is stored, and markup in a crafted caller id never reaches a
/// page that shows it.
/// </summary>
public static class CallerNumbers
{
    private static readonly string[] _anonymousMarkers =
    [
        "anonymous",
        "restricted",
        "unknown",
        "private",
        "unavailable",
    ];

    /// <summary>
    /// Returns the caller id with only digits, <c>+</c>, <c>*</c> and <c>#</c> kept, or a known anonymous marker as it
    /// was given. A SIP address (<c>sip:</c> or <c>sips:</c>) stays an address with only the characters such an address
    /// is written with, none of which markup needs. Returns <see langword="null"/> when nothing dialable is left.
    /// </summary>
    /// <param name="value">The caller id as the carrier or the PBX reported it.</param>
    /// <returns>The cleaned caller id.</returns>
    public static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (_anonymousMarkers.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.StartsWith("sip:", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("sips:", StringComparison.OrdinalIgnoreCase))
        {
            var address = new string([.. trimmed.Where(IsSipAddressCharacter)]);

            return address.EndsWith(':') ? null : address;
        }

        var kept = new string([.. trimmed.Where(character => char.IsAsciiDigit(character) || character is '+' or '*' or '#')]);

        return kept.Length == 0 ? null : kept;
    }

    // Letters, digits and the separators a SIP address is written with; no quote, angle bracket, space or parenthesis.
    private static bool IsSipAddressCharacter(char character)
        => char.IsAsciiLetterOrDigit(character) || character is ':' or '@' or '.' or '-' or '_' or '+' or '*' or '#' or ';' or '=' or '%' or '~' or '!';
}
