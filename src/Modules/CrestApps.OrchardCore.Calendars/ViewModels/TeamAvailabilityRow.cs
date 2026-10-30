namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// One member's day on the team availability page.
/// </summary>
public class TeamAvailabilityRow
{
    /// <summary>
    /// Gets or sets the member's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets, for each half hour shown, whether the member is busy (2), tentatively busy (1) or free (0).
    /// </summary>
    public IList<int> Slots { get; set; } = [];
}
