namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// The ratings the lead Rating field starts with. They can be changed in the field's predefined list.
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
}
