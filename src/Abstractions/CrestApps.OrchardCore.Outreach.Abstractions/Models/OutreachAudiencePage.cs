namespace CrestApps.OrchardCore.Outreach.Models;

/// <summary>
/// One page of an outreach run's audience.
/// </summary>
public sealed class OutreachAudiencePage
{
    /// <summary>
    /// An empty last page.
    /// </summary>
    public static OutreachAudiencePage Empty { get; } = new();

    /// <summary>
    /// Gets the recipients on the page.
    /// </summary>
    public IReadOnlyList<OutreachRecipient> Recipients { get; init; } = [];

    /// <summary>
    /// Gets the cursor of the next page, or <see langword="null"/> when this is the last one.
    /// </summary>
    public string NextCursor { get; init; }
}
