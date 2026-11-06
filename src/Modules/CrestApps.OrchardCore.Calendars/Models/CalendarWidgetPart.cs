using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// Makes an admin dashboard widget show calendars: the viewer's own, every calendar of a type, or calendars the widget
/// names. Each viewer sees only the calendars they may open, so one widget can serve everyone. A dashboard can hold as
/// many calendar widgets as it likes, each with its own calendars and view.
/// </summary>
public sealed class CalendarWidgetPart : ContentPart
{
    /// <summary>
    /// The name of the part.
    /// </summary>
    public const string PartName = nameof(CalendarWidgetPart);

    /// <summary>
    /// The name of the dashboard widget content type.
    /// </summary>
    public const string WidgetType = "CalendarWidget";

    /// <summary>
    /// Gets or sets which calendars the widget shows.
    /// </summary>
    public CalendarWidgetSource Source { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the calendar type whose calendars the widget shows, when
    /// <see cref="Source"/> is <see cref="CalendarWidgetSource.Type"/>.
    /// </summary>
    public string CalendarType { get; set; }

    /// <summary>
    /// Gets or sets the identifiers of the calendars the widget shows, when <see cref="Source"/> is
    /// <see cref="CalendarWidgetSource.Calendars"/>.
    /// </summary>
    public string[] CalendarIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the view the widget opens on.
    /// </summary>
    public CalendarWidgetView View { get; set; }
}

/// <summary>
/// Which calendars a dashboard calendar widget shows.
/// </summary>
public enum CalendarWidgetSource
{
    /// <summary>
    /// The viewer's own calendars: their personal calendar and the calendars they may add events to, as the calendar
    /// page shows them first.
    /// </summary>
    MyCalendars,

    /// <summary>
    /// Every calendar of one type that the viewer may open.
    /// </summary>
    Type,

    /// <summary>
    /// The calendars the widget names, of those the viewer may open.
    /// </summary>
    Calendars,
}

/// <summary>
/// The view a dashboard calendar widget opens on.
/// </summary>
public enum CalendarWidgetView
{
    /// <summary>
    /// A list of the week's events.
    /// </summary>
    Agenda,

    /// <summary>
    /// One day, hour by hour.
    /// </summary>
    Day,

    /// <summary>
    /// One week, hour by hour.
    /// </summary>
    Week,

    /// <summary>
    /// A month.
    /// </summary>
    Month,
}
