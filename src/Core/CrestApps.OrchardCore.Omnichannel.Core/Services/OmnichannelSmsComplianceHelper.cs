namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Provides SMS compliance helpers for automated omnichannel conversations.
/// </summary>
public static class OmnichannelSmsComplianceHelper
{
    /// <summary>
    /// Gets the default SMS opt-out keywords.
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
    /// Determines whether an inbound SMS message is an opt-out request.
    /// </summary>
    /// <param name="message">The inbound message body.</param>
    /// <param name="keywords">The configured opt-out keywords.</param>
    public static bool IsOptOutRequest(string message, IEnumerable<string> keywords = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalizedMessage = message.Trim();
        var normalizedKeywords = NormalizeOptOutKeywords(keywords);

        foreach (var keyword in normalizedKeywords)
        {
            if (!normalizedMessage.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (normalizedMessage.Length == keyword.Length ||
                !char.IsLetterOrDigit(normalizedMessage[keyword.Length]))
            {
                return true;
            }
        }

        return false;
    }
}
