using CrestApps.OrchardCore.Calendars.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Describes a kind of event a calendar type holds, such as a shift or time off on a workforce calendar.
/// </summary>
public sealed class CalendarEventTypeDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventTypeDescriptor"/> class.
    /// </summary>
    /// <param name="name">The technical name of the event type, stored on each event.</param>
    /// <param name="displayName">The name shown for the event type.</param>
    public CalendarEventTypeDescriptor(string name, LocalizationSource displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        DisplayName = displayName ?? new LocalizationSource(name);
    }

    /// <summary>
    /// Gets the technical name of the event type.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the name shown for the event type.
    /// </summary>
    public LocalizationSource DisplayName { get; }

    /// <summary>
    /// Gets or sets the color events of the type are drawn in, or <see langword="null"/> to use the calendar's color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the Font Awesome classes of the event type's icon.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Gets or sets whether new events of the type take time (busy) or leave it free.
    /// </summary>
    public CalendarTransparency DefaultTransparency { get; set; } = CalendarTransparency.Busy;

    /// <summary>
    /// Gets or sets whether new events of the type last whole days.
    /// </summary>
    public bool AllDayByDefault { get; set; }

    /// <summary>
    /// Gets or sets how long new events of the type last, in minutes.
    /// </summary>
    public int DefaultDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets whether a new event of the type waits for approval: it is created tentative and someone who can manage the
    /// calendar confirms it.
    /// </summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// Gets or sets whether an event of the type must name at least one attendee, such as the employee working a shift.
    /// </summary>
    public bool RequiresAttendee { get; set; }

    /// <summary>
    /// Gets or sets whether a busy event of the type counts toward the calendar's capacity.
    /// </summary>
    public bool CountsTowardCapacity { get; set; } = true;
}
