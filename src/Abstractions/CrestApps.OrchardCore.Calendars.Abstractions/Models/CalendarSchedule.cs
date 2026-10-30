namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// A person's schedule, or everyone's: the resource to check <see cref="CalendarPermissions.ListCalendarEvents"/> with.
/// </summary>
public sealed class CalendarSchedule
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarSchedule"/> class.
    /// </summary>
    /// <param name="userId">The person, or <see langword="null"/> for everyone.</param>
    public CalendarSchedule(string userId)
    {
        UserId = string.IsNullOrWhiteSpace(userId) ? null : userId;
    }

    /// <summary>
    /// Gets everyone's schedule.
    /// </summary>
    public static CalendarSchedule Everyone { get; } = new(null);

    /// <summary>
    /// Gets the person whose schedule it is, or <see langword="null"/> for everyone.
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// Gets whether it is everyone's schedule.
    /// </summary>
    public bool IsEveryone => UserId is null;
}
