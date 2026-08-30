using System.Globalization;
using CrestApps.Core.AI.Security;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Identifies why a rate-limit tier line could not be parsed.
/// </summary>
internal enum ChatRateLimitTierParseErrorKind
{
    /// <summary>
    /// The line does not contain the comma that separates the limit from the window.
    /// </summary>
    MissingSeparator,

    /// <summary>
    /// The limit portion of the line is not a positive whole number.
    /// </summary>
    InvalidLimit,

    /// <summary>
    /// The window portion of the line is not a positive <see cref="TimeSpan"/>.
    /// </summary>
    InvalidWindow,
}

/// <summary>
/// Describes the first invalid line found while parsing rate-limit tiers.
/// </summary>
/// <param name="Kind">The reason the line is invalid.</param>
/// <param name="LineNumber">The one-based number of the offending non-blank line.</param>
/// <param name="Value">The offending portion of the line, or the whole line when no separator was found.</param>
internal sealed record ChatRateLimitTierParseError(ChatRateLimitTierParseErrorKind Kind, int LineNumber, string Value);

/// <summary>
/// Converts a list of <see cref="ChatRateLimitTier"/> to and from the multi-line text format used by
/// the admin settings editors. Each tier is one line formatted as <c>limit, window</c>, where the
/// window is a <see cref="TimeSpan"/> string such as <c>00:00:30</c>, <c>01:00:00</c>, or
/// <c>1.00:00:00</c> (a day).
/// </summary>
internal static class ChatRateLimitTierTextFormatter
{
    /// <summary>
    /// Formats the tiers as one <c>limit, window</c> line per tier.
    /// </summary>
    /// <param name="tiers">The tiers to format.</param>
    /// <returns>The multi-line representation, or an empty string when there is nothing to format.</returns>
    public static string Format(IEnumerable<ChatRateLimitTier> tiers)
    {
        if (tiers is null)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            tiers
                .Where(tier => tier is not null)
                .Select(tier => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{tier.Limit}, {tier.Window.ToString("c", CultureInfo.InvariantCulture)}")));
    }

    /// <summary>
    /// Formats the tiers on a single line, separated by semicolons, for use in hints and placeholders
    /// where a multi-line value cannot be rendered.
    /// </summary>
    /// <param name="tiers">The tiers to format.</param>
    /// <returns>The single-line representation, or an empty string when there is nothing to format.</returns>
    public static string FormatInline(IEnumerable<ChatRateLimitTier> tiers)
        => Format(tiers).Replace(Environment.NewLine, "; ");

    /// <summary>
    /// Builds a localized message describing a parse error.
    /// </summary>
    /// <param name="error">The parse error to describe.</param>
    /// <param name="S">The string localizer.</param>
    /// <returns>The localized message.</returns>
    public static LocalizedString Describe(ChatRateLimitTierParseError error, IStringLocalizer S)
        => error.Kind switch
        {
            ChatRateLimitTierParseErrorKind.InvalidLimit => S["Line {0}: '{1}' is not a positive whole number.", error.LineNumber, error.Value],
            ChatRateLimitTierParseErrorKind.InvalidWindow => S["Line {0}: '{1}' is not a valid window. Use hh:mm:ss, for example 00:00:30 or 1.00:00:00 for a day.", error.LineNumber, error.Value],
            _ => S["Line {0}: use the format 'limit, window', for example '5, 00:00:30'.", error.LineNumber],
        };

    /// <summary>
    /// Parses the multi-line text into tiers. Blank lines are ignored. Returns <see langword="true"/>
    /// when every non-blank line is valid; otherwise <paramref name="error"/> describes the first
    /// problem and <paramref name="tiers"/> is empty.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="tiers">The parsed tiers, or an empty list when the text is blank or invalid.</param>
    /// <param name="error">The first validation error, or <see langword="null"/> when valid.</param>
    /// <returns><see langword="true"/> when the text is valid; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string text, out List<ChatRateLimitTier> tiers, out ChatRateLimitTierParseError error)
    {
        tiers = [];
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var lineNumber = 0;

        foreach (var line in lines)
        {
            lineNumber++;

            var separatorIndex = line.IndexOf(',');

            if (separatorIndex < 0)
            {
                error = new ChatRateLimitTierParseError(ChatRateLimitTierParseErrorKind.MissingSeparator, lineNumber, line);
                tiers = [];

                return false;
            }

            var limitText = line[..separatorIndex].Trim();
            var windowText = line[(separatorIndex + 1)..].Trim();

            if (!int.TryParse(limitText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) || limit <= 0)
            {
                error = new ChatRateLimitTierParseError(ChatRateLimitTierParseErrorKind.InvalidLimit, lineNumber, limitText);
                tiers = [];

                return false;
            }

            if (!TimeSpan.TryParse(windowText, CultureInfo.InvariantCulture, out var window) || window <= TimeSpan.Zero)
            {
                error = new ChatRateLimitTierParseError(ChatRateLimitTierParseErrorKind.InvalidWindow, lineNumber, windowText);
                tiers = [];

                return false;
            }

            tiers.Add(new ChatRateLimitTier
            {
                Limit = limit,
                Window = window,
            });
        }

        return true;
    }

    /// <summary>
    /// Determines whether two tier lists describe the same limits in the same order.
    /// </summary>
    /// <param name="left">The first list.</param>
    /// <param name="right">The second list.</param>
    /// <returns><see langword="true"/> when both lists are equivalent; otherwise, <see langword="false"/>.</returns>
    public static bool AreEquivalent(IReadOnlyList<ChatRateLimitTier> left, IReadOnlyList<ChatRateLimitTier> right)
    {
        var leftCount = left?.Count ?? 0;
        var rightCount = right?.Count ?? 0;

        if (leftCount != rightCount)
        {
            return false;
        }

        for (var i = 0; i < leftCount; i++)
        {
            if (left[i].Limit != right[i].Limit || left[i].Window != right[i].Window)
            {
                return false;
            }
        }

        return true;
    }
}
