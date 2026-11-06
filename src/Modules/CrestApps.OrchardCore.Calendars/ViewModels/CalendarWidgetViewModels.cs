using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// What a dashboard calendar widget shows.
/// </summary>
public class CalendarWidgetViewModel
{
    /// <summary>
    /// Gets or sets whether the viewer may use calendars at all.
    /// </summary>
    public bool CanView { get; set; }

    /// <summary>
    /// Gets or sets whether the widget names calendars to show; one that names none asks to be edited.
    /// </summary>
    public bool IsConfigured { get; set; }

    /// <summary>
    /// Gets or sets the calendars the viewer sees in the widget.
    /// </summary>
    public IList<CalendarViewCalendar> Calendars { get; set; } = [];

    /// <summary>
    /// Gets or sets what the widget's script needs, as JSON.
    /// </summary>
    public string ConfigurationJson { get; set; }

    /// <summary>
    /// Gets or sets the view of the calendar page that matches the widget's view.
    /// </summary>
    public string PageView { get; set; }
}

/// <summary>
/// The editor of a dashboard calendar widget.
/// </summary>
public class CalendarWidgetEditViewModel
{
    /// <summary>
    /// Gets or sets which calendars the widget shows.
    /// </summary>
    public CalendarWidgetSource Source { get; set; }

    /// <summary>
    /// Gets or sets the calendar type whose calendars the widget shows.
    /// </summary>
    public string CalendarType { get; set; }

    /// <summary>
    /// Gets or sets the calendars the widget shows.
    /// </summary>
    public string[] CalendarIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the view the widget opens on.
    /// </summary>
    public CalendarWidgetView View { get; set; }

    /// <summary>
    /// Gets or sets the calendar types the editor may choose from.
    /// </summary>
    public IList<SelectListItem> Types { get; set; } = [];

    /// <summary>
    /// Gets or sets the calendars the editor may choose from.
    /// </summary>
    public IList<SelectListItem> AvailableCalendars { get; set; } = [];

    /// <summary>
    /// Gets or sets the views the editor may choose from.
    /// </summary>
    public IList<SelectListItem> Views { get; set; } = [];
}
