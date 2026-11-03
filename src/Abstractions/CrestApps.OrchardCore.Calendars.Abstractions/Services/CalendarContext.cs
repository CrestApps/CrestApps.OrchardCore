using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// A calendar being changed.
/// </summary>
public class CalendarContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarContext"/> class.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="type">The calendar's type, or <see langword="null"/> when its type is no longer registered.</param>
    /// <param name="options">How the change is made.</param>
    public CalendarContext(
        Calendar calendar,
        CalendarTypeDescriptor type,
        CalendarChangeOptions options)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        Calendar = calendar;
        Type = type;
        Options = options ?? new CalendarChangeOptions();
    }

    /// <summary>
    /// Gets the calendar.
    /// </summary>
    public Calendar Calendar { get; }

    /// <summary>
    /// Gets the calendar's type.
    /// </summary>
    public CalendarTypeDescriptor Type { get; }

    /// <summary>
    /// Gets how the change is made.
    /// </summary>
    public CalendarChangeOptions Options { get; }
}
