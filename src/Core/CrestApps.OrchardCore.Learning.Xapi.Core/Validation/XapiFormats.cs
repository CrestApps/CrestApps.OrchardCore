using System.Globalization;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Validation;

/// <summary>
/// The value formats of xAPI 1.0.3: IRIs and IRLs, UUIDs, RFC 5646 language tags, ISO 8601 timestamps and durations,
/// <c>mailto:</c> addresses and SHA-1 and SHA-2 hashes.
/// </summary>
public static partial class XapiFormats
{
    /// <summary>
    /// Gets whether a value is an absolute IRI: a scheme, no white space, and a valid absolute URI once its characters
    /// outside ASCII are allowed.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for an absolute IRI.</returns>
    public static bool IsIri(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 2048 || value.Any(char.IsWhiteSpace) || !SchemeRegex().IsMatch(value))
        {
            return false;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Scheme);
    }

    /// <summary>
    /// Gets whether a value is an IRL: an absolute IRI that can be looked up (here, any absolute IRI).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for an IRL.</returns>
    public static bool IsIrl(string value)
        => IsIri(value);

    /// <summary>
    /// Gets whether a value is a UUID in the standard string form (8-4-4-4-12 hex digits).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for a UUID.</returns>
    public static bool IsUuid(string value)
        => !string.IsNullOrEmpty(value) && UuidRegex().IsMatch(value);

    /// <summary>
    /// Gets whether a value is an RFC 5646 language tag (a primary subtag of 1 to 8 letters and subtags of 1 to 8
    /// letters or digits, which covers private use <c>x-</c> and grandfathered <c>i-</c> tags).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for a language tag.</returns>
    public static bool IsLanguageTag(string value)
        => !string.IsNullOrEmpty(value) && value.Length <= 64 && LanguageTagRegex().IsMatch(value);

    /// <summary>
    /// Reads an ISO 8601 timestamp: a full date and time, with an optional fraction and an optional zone (a value
    /// without a zone is UTC). The zone <c>-00:00</c> is refused, as xAPI requires.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="utc">The time in UTC.</param>
    /// <returns><see langword="true"/> when the value is a valid timestamp.</returns>
    public static bool TryParseTimestamp(string value, out DateTime utc)
    {
        utc = default;

        if (string.IsNullOrEmpty(value) || value.Length > 64 || !TimestampRegex().IsMatch(value) || value.EndsWith("-00:00", StringComparison.Ordinal) || value.EndsWith("-0000", StringComparison.Ordinal))
        {
            return false;
        }

        var hasZone = value.EndsWith('Z') || value.EndsWith('z') || ZoneRegex().IsMatch(value);
        var text = hasZone ? value : value + "Z";

        // Fractions longer than seven digits do not parse, but xAPI allows any precision: keep seven.
        text = FractionRegex().Replace(text, match => match.Value.Length > 8 ? match.Value[..8] : match.Value);

        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return false;
        }

        utc = parsed.UtcDateTime;

        return true;
    }

    /// <summary>
    /// Gets whether a value is an ISO 8601 duration, such as <c>PT1H30M</c> or <c>P1DT4.5S</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for a duration.</returns>
    public static bool IsDuration(string value)
        => !string.IsNullOrEmpty(value) && value.Length <= 64 && value != "P" && !value.EndsWith('T') && DurationRegex().IsMatch(value);

    /// <summary>
    /// Gets whether a value is a <c>mailto:</c> IRI of an e-mail address.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for a mailbox IRI.</returns>
    public static bool IsMailbox(string value)
        => !string.IsNullOrEmpty(value) && value.Length <= 320 && MailboxRegex().IsMatch(value);

    /// <summary>
    /// Gets whether a value is a SHA-1 hash in hex.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for 40 hex digits.</returns>
    public static bool IsSha1(string value)
        => !string.IsNullOrEmpty(value) && value.Length == 40 && value.All(Uri.IsHexDigit);

    /// <summary>
    /// Gets whether a value is a SHA-2 hash in hex (SHA-224, SHA-256, SHA-384 or SHA-512).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for 56, 64, 96 or 128 hex digits.</returns>
    public static bool IsSha2(string value)
        => !string.IsNullOrEmpty(value) && value.Length is 56 or 64 or 96 or 128 && value.All(Uri.IsHexDigit);

    /// <summary>
    /// Formats a time in UTC as xAPI returns it: ISO 8601 with milliseconds and <c>Z</c>.
    /// </summary>
    /// <param name="utc">The time.</param>
    /// <returns>The text.</returns>
    public static string FormatTimestamp(DateTime utc)
        => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// Cuts a time to the millisecond, the precision the LRS keeps and returns.
    /// </summary>
    /// <param name="utc">The time.</param>
    /// <returns>The time to the millisecond, in UTC.</returns>
    public static DateTime ToMilliseconds(DateTime utc)
        => DateTime.SpecifyKind(new DateTime(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMillisecond)), DateTimeKind.Utc);

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.\-]*:")]
    private static partial Regex SchemeRegex();

    [GeneratedRegex(@"^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$")]
    private static partial Regex UuidRegex();

    [GeneratedRegex(@"^[A-Za-z]{1,8}(-[A-Za-z0-9]{1,8})*$")]
    private static partial Regex LanguageTagRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|z|[+\-]\d{2}(:?\d{2})?)?$")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"[+\-]\d{2}(:?\d{2})?$")]
    private static partial Regex ZoneRegex();

    [GeneratedRegex(@"\.\d+")]
    private static partial Regex FractionRegex();

    [GeneratedRegex(@"^P(\d+(\.\d+)?Y)?(\d+(\.\d+)?M)?(\d+(\.\d+)?W)?(\d+(\.\d+)?D)?(T(\d+(\.\d+)?H)?(\d+(\.\d+)?M)?(\d+(\.\d+)?S)?)?$")]
    private static partial Regex DurationRegex();

    [GeneratedRegex(@"^mailto:[^@\s:/]+@[^@\s/]+$")]
    private static partial Regex MailboxRegex();
}
