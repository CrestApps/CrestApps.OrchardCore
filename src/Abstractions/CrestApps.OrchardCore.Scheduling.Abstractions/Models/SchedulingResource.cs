using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// Something that takes appointments: a staff member, a room or a piece of equipment.
/// </summary>
public sealed class SchedulingResource : CatalogItem
{
    /// <summary>
    /// Gets or sets the resource's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what kind of resource it is.
    /// </summary>
    public SchedulingResourceKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the user a staff member is, whose own calendar time also counts as busy.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the calendar the resource's appointments go on, and whose events make it busy.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the time zone the working hours are in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets where its available hours come from.
    /// </summary>
    public SchedulingAvailabilityMode AvailabilityMode { get; set; }

    /// <summary>
    /// Gets or sets the workforce calendar whose shifts the resource works, for the Shifts mode.
    /// </summary>
    public string ShiftCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the weekly working hours.
    /// </summary>
    public IList<SchedulingWorkingHours> WorkingHours { get; set; } = [];

    /// <summary>
    /// Gets or sets the date overrides.
    /// </summary>
    public IList<SchedulingDateOverride> DateOverrides { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the resource takes appointments.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a counter raised with every booking; saved with a concurrency check so two bookings can't take the same time.
    /// </summary>
    public long BookingVersion { get; set; }
}
