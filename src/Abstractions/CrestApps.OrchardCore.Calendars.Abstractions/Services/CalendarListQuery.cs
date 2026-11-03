namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// What a calendar list matches.
/// </summary>
public sealed class CalendarListQuery
{
    /// <summary>
    /// Gets or sets the text the name must contain.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the calendar types to match, or none for every type.
    /// </summary>
    public IList<string> Types { get; set; } = [];

    /// <summary>
    /// Gets or sets the owner id to match.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the owner ids to match, such as a user's id and the names of their roles.
    /// </summary>
    public IList<string> OwnerIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the managing module to match.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets whether to match only enabled, or only disabled, calendars.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    /// Gets or sets the most calendars <see cref="ICalendarService.ListAsync"/> returns.
    /// </summary>
    public int Limit { get; set; } = 1000;
}
