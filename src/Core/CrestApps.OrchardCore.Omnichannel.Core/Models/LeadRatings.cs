namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// The ratings a lead can carry.
/// </summary>
public static class LeadRatings
{
    public const string Hot = "Hot";

    public const string Warm = "Warm";

    public const string Cold = "Cold";

    /// <summary>
    /// Gets every rating, hottest first.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Hot, Warm, Cold];

    /// <summary>
    /// Returns the rating that matches the value regardless of case, or <see langword="null"/> when the value is
    /// not a rating.
    /// </summary>
    /// <param name="value">The value to match.</param>
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return All.FirstOrDefault(rating => string.Equals(rating, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
