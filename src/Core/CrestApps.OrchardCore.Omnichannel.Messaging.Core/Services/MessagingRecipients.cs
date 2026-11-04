namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Reads the recipient addresses an agent typed into a composer or a broadcast.
/// </summary>
public static class MessagingRecipients
{
    private static readonly char[] _separators = ['\n', '\r', ',', ';'];

    /// <summary>
    /// Splits typed text into addresses: one per line, or separated by commas or semicolons, trimmed, with repeats
    /// (compared without case) removed and the first order kept.
    /// </summary>
    /// <param name="text">The typed text.</param>
    /// <returns>The addresses, or an empty list when there are none.</returns>
    public static List<string> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
