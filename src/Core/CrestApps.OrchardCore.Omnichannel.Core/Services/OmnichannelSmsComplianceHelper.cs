namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// The one SMS keyword policy: decides whether an inbound text is a carrier keyword (an opt-out, a request for help or
/// an opt-in). The automated conversation and the messaging workspace both ask it, so a text means the same thing on
/// every path.
/// </summary>
/// <remarks>
/// <para>
/// The message is trimmed, trailing punctuation (<c>. ! ? , ; :</c>) is removed, and the rest is compared with each
/// keyword without regard to case. An opt-out is the safe side of a doubt, so a message opts out when it is an opt-out
/// keyword or starts with one followed by a space or punctuation: "STOP", "stop." and "STOP please" all opt out, while
/// "stopping by" and "please stop" do not. A request for help and an opt-in must be the whole message, so "help me
/// find my order" or "start of the month" is an ordinary reply.
/// </para>
/// <para>
/// The standard opt-out keywords (<see cref="DefaultOptOutKeywords"/>) always opt out. Keywords a tenant configures
/// for a subject flow are honored in addition to them, never instead of them.
/// </para>
/// </remarks>
public static class OmnichannelSmsComplianceHelper
{
    private static readonly char[] _trailingPunctuation = ['.', '!', '?', ',', ';', ':'];

    /// <summary>
    /// Gets the standard SMS opt-out keywords: STOP, STOPALL, UNSUBSCRIBE, CANCEL, END and QUIT.
    /// </summary>
    public static IReadOnlyList<string> DefaultOptOutKeywords { get; } =
    [
        "STOP",
        "STOPALL",
        "UNSUBSCRIBE",
        "CANCEL",
        "END",
        "QUIT",
    ];

    /// <summary>
    /// Gets the keywords that ask who is texting: HELP and INFO.
    /// </summary>
    public static IReadOnlyList<string> HelpKeywords { get; } =
    [
        "HELP",
        "INFO",
    ];

    /// <summary>
    /// Gets the keywords that opt a contact back in: START, UNSTOP and YES.
    /// </summary>
    public static IReadOnlyList<string> OptInKeywords { get; } =
    [
        "START",
        "UNSTOP",
        "YES",
    ];

    /// <summary>
    /// Parses opt-out keywords from comma, semicolon, or line-separated text.
    /// </summary>
    /// <param name="keywords">The keyword text.</param>
    public static IReadOnlyList<string> ParseOptOutKeywords(string keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return DefaultOptOutKeywords;
        }

        return NormalizeOptOutKeywords(keywords.Split(
            [',', ';', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>
    /// Normalizes opt-out keywords and falls back to the default keyword list when no keywords are supplied.
    /// </summary>
    /// <param name="keywords">The keywords to normalize.</param>
    public static IReadOnlyList<string> NormalizeOptOutKeywords(IEnumerable<string> keywords)
    {
        var normalizedKeywords = keywords?
            .Select(keyword => keyword?.Trim())
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalizedKeywords is { Length: > 0 }
            ? normalizedKeywords
            : DefaultOptOutKeywords;
    }

    /// <summary>
    /// Determines whether an inbound SMS message is an opt-out request: the message is, or starts with, one of the
    /// standard opt-out keywords or one of the configured keywords, followed by a space or punctuation.
    /// </summary>
    /// <param name="message">The inbound message body.</param>
    /// <param name="keywords">The keywords a tenant configured in addition to the standard ones, if any.</param>
    public static bool IsOptOutRequest(string message, IEnumerable<string> keywords = null)
    {
        var normalized = NormalizeMessage(message);

        if (normalized is null)
        {
            return false;
        }

        return StartsWithKeyword(normalized, DefaultOptOutKeywords) ||
            (keywords is not null && StartsWithKeyword(normalized, NormalizeOptOutKeywords(keywords)));
    }

    /// <summary>
    /// Determines whether an inbound SMS message asks who is texting: the whole message is one of the
    /// <see cref="HelpKeywords"/>.
    /// </summary>
    /// <param name="message">The inbound message body.</param>
    public static bool IsHelpRequest(string message)
    {
        var normalized = NormalizeMessage(message);

        return normalized is not null && Matches(normalized, HelpKeywords);
    }

    /// <summary>
    /// Determines whether an inbound SMS message opts the contact back in: the whole message is one of the
    /// <see cref="OptInKeywords"/>.
    /// </summary>
    /// <param name="message">The inbound message body.</param>
    public static bool IsOptInRequest(string message)
    {
        var normalized = NormalizeMessage(message);

        return normalized is not null && Matches(normalized, OptInKeywords);
    }

    private static string NormalizeMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var normalized = message.Trim().TrimEnd(_trailingPunctuation).TrimEnd();

        return normalized.Length == 0
            ? null
            : normalized;
    }

    private static bool Matches(string normalized, IEnumerable<string> keywords)
        => keywords.Any(keyword => string.Equals(normalized, keyword, StringComparison.OrdinalIgnoreCase));

    // The message is the keyword, or starts with it and the keyword ends there: "STOP please" and "STOP, thanks" match
    // STOP, while "STOPPING" does not.
    private static bool StartsWithKeyword(string normalized, IEnumerable<string> keywords)
        => keywords.Any(keyword =>
            normalized.StartsWith(keyword, StringComparison.OrdinalIgnoreCase) &&
            (normalized.Length == keyword.Length || !char.IsLetterOrDigit(normalized[keyword.Length])));
}
