using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Scoring;

/// <summary>
/// Helpers shared by the scorers.
/// </summary>
internal static partial class ScoringText
{
    /// <summary>
    /// Trims a text and collapses its runs of white space, so <c>"  city  of light "</c> matches <c>"city of light"</c>.
    /// </summary>
    public static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : Spaces().Replace(value.Trim(), " ");

    /// <summary>
    /// Compares two texts after <see cref="Normalize"/>.
    /// </summary>
    public static bool Same(string left, string right, bool caseSensitive)
        => string.Equals(Normalize(left), Normalize(right), caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Rounds points to two decimals, never below zero or above the points possible.
    /// </summary>
    public static decimal Points(decimal earned, decimal possible)
        => Math.Round(Math.Clamp(earned, 0, Math.Max(possible, 0)), 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Returns the items in a random order.
    /// </summary>
    public static List<T> Shuffle<T>(IEnumerable<T> items, Random random)
    {
        var list = items.ToList();

        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
