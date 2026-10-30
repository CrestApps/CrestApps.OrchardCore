namespace CrestApps.OrchardCore.Calendars.Core.ICalendar;

/// <summary>
/// What an iCalendar import did.
/// </summary>
public sealed class ICalendarImportResult
{
    /// <summary>
    /// Gets or sets how many events were added.
    /// </summary>
    public int Created { get; set; }

    /// <summary>
    /// Gets or sets how many events were changed.
    /// </summary>
    public int Updated { get; set; }

    /// <summary>
    /// Gets or sets how many events were removed because the source no longer has them.
    /// </summary>
    public int Removed { get; set; }

    /// <summary>
    /// Gets or sets how many events were left alone because nothing changed.
    /// </summary>
    public int Unchanged { get; set; }

    /// <summary>
    /// Gets the events that were refused, with why.
    /// </summary>
    public IList<string> Errors { get; } = [];
}
