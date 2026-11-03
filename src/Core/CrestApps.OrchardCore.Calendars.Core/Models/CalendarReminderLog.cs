using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Calendars.Core.Models;

/// <summary>
/// A record that a reminder was raised, so it is raised only once.
/// </summary>
public sealed class CalendarReminderLog : CatalogItem
{
    /// <summary>
    /// Gets or sets the key that identifies the reminder: its kind, the event, the occurrence's start and the offset.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the event the reminder was raised for.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets when the reminder was raised.
    /// </summary>
    public DateTime RaisedUtc { get; set; }
}
