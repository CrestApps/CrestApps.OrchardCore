using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Core.ICalendar;

/// <summary>
/// How an iCalendar import is made.
/// </summary>
public sealed class ICalendarImportOptions
{
    /// <summary>
    /// Gets or sets who makes the import.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets the source imported events are marked with, and the only events the import may change or remove; or
    /// <see langword="null"/> for a one-off file import that matches any event.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets whether imported events may be changed in the calendar.
    /// </summary>
    public bool Editable { get; set; } = true;

    /// <summary>
    /// Gets or sets whether events no longer in the document are removed, for a feed subscription.
    /// </summary>
    public bool RemoveMissing { get; set; }

    /// <summary>
    /// Gets or sets whether to skip the calendar's overlap limit, for a copy of another calendar.
    /// </summary>
    public bool SkipConflictCheck { get; set; }

    /// <summary>
    /// Gets or sets whether workflows and other observers hear about the imported changes.
    /// </summary>
    public bool Notify { get; set; } = true;

    /// <summary>
    /// Gets or sets a check that decides whether an existing event may be overwritten with the imported copy, or
    /// <see langword="null"/> to always overwrite. External synchronization uses it to keep local changes that win a conflict.
    /// </summary>
    public Func<CalendarEvent, ICalendarEventData, bool> CanUpdate { get; set; }

    /// <summary>
    /// Gets or sets a check that decides whether an event no longer in the document may be removed, or <see langword="null"/>
    /// to always remove it when <see cref="RemoveMissing"/> is on.
    /// </summary>
    public Func<CalendarEvent, bool> CanRemove { get; set; }

    /// <summary>
    /// Gets or sets an action that runs on each event after the imported copy is applied and before it is saved, such as
    /// recording which external event it came from.
    /// </summary>
    public Action<CalendarEvent, ICalendarEventData> Applied { get; set; }
}
