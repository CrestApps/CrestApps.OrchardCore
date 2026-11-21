using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// Helpers shared by the scorers: text comparison and point rounding.
/// </summary>
public static partial class SurveyScoringMath
{
    /// <summary>
    /// Trims a text and folds its runs of white space into one space.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The normalized text.</returns>
    public static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : Spaces().Replace(value.Trim(), " ");

    /// <summary>
    /// Compares two texts after normalizing them.
    /// </summary>
    /// <param name="left">The first text.</param>
    /// <param name="right">The second text.</param>
    /// <param name="caseSensitive">Whether case matters.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    public static bool Same(string left, string right, bool caseSensitive)
        => string.Equals(Normalize(left), Normalize(right), caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Clamps earned points between 0 and the points possible, rounded to two decimals.
    /// </summary>
    /// <param name="earned">The points earned.</param>
    /// <param name="possible">The points possible.</param>
    /// <returns>The points.</returns>
    public static decimal Points(decimal earned, decimal possible)
        => Math.Round(Math.Clamp(earned, 0, Math.Max(possible, 0)), 2, MidpointRounding.AwayFromZero);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
