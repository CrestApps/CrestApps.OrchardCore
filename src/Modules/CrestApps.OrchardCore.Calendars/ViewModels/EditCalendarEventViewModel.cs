using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The page that creates or changes an event.
/// </summary>
public class EditCalendarEventViewModel
{
    /// <summary>
    /// Gets or sets the event's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the calendar's id.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the occurrence being changed, for one occurrence of a repeating event.
    /// </summary>
    public string Occurrence { get; set; }

    /// <summary>
    /// Gets or sets which occurrences of a repeating event the change applies to.
    /// </summary>
    public string Scope { get; set; } = "ThisOccurrence";

    /// <summary>
    /// Gets or sets the page title.
    /// </summary>
    [BindNever]
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the calendar's name.
    /// </summary>
    [BindNever]
    public string CalendarName { get; set; }

    /// <summary>
    /// Gets or sets whether the event is new.
    /// </summary>
    [BindNever]
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets whether one occurrence of a repeating event is being changed.
    /// </summary>
    [BindNever]
    public bool IsOccurrence { get; set; }

    /// <summary>
    /// Gets or sets whether the event waits for approval and the viewer may confirm it.
    /// </summary>
    [BindNever]
    public bool CanConfirm { get; set; }

    /// <summary>
    /// Gets or sets the viewer's own answer, when the viewer is invited.
    /// </summary>
    [BindNever]
    public string MyResponse { get; set; }

    /// <summary>
    /// Gets or sets the module that owns the event.
    /// </summary>
    [BindNever]
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the editor shape.
    /// </summary>
    [BindNever]
    public dynamic Editor { get; set; }
}
