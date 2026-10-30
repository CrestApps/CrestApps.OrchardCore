using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// A calendar being validated.
/// </summary>
public sealed class CalendarValidatingContext : CalendarContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarValidatingContext"/> class.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="type">The calendar's type.</param>
    /// <param name="options">How the change is made.</param>
    /// <param name="isNew">Whether the calendar is being created.</param>
    public CalendarValidatingContext(
        Calendar calendar,
        CalendarTypeDescriptor type,
        CalendarChangeOptions options,
        bool isNew)
        : base(calendar, type, options)
    {
        IsNew = isNew;
    }

    /// <summary>
    /// Gets whether the calendar is being created.
    /// </summary>
    public bool IsNew { get; }

    /// <summary>
    /// Gets the errors found so far. Add one to refuse the save.
    /// </summary>
    public IList<CalendarError> Errors { get; } = [];
}
