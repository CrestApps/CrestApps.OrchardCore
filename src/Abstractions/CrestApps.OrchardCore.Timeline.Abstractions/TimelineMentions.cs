using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Timeline;

/// <summary>
/// Reads the <c>@username</c> mentions in a timeline entry's text, the one rule every module uses, so they all agree on
/// who was mentioned.
/// </summary>
public static partial class TimelineMentions
{
    /// <summary>
    /// The most mentions read from one text.
    /// </summary>
    public const int MaxMentions = 20;

    /// <summary>
    /// Finds the user names a text mentions with <c>@username</c>, each once. An e-mail address is not a mention.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The user names.</returns>
    public static IReadOnlyList<string> Find(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return [.. MentionPattern().Matches(text)
            .Select(match => match.Groups["name"].Value.TrimEnd('.', '-', '_'))
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxMentions)];
    }

    /// <summary>
    /// Gets whether a text mentions a user.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="userName">The user's name.</param>
    /// <returns><see langword="true"/> when the text mentions the user.</returns>
    public static bool Mentions(string text, string userName)
        => !string.IsNullOrEmpty(userName) && Find(text).Contains(userName, StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"(?<![\w@.])@(?<name>[A-Za-z0-9][A-Za-z0-9._\-]*)", RegexOptions.CultureInvariant)]
    private static partial Regex MentionPattern();
}
