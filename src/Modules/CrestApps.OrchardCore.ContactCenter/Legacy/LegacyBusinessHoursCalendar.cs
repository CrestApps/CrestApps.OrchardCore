using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Legacy;

/// <summary>
/// A business hours calendar as the Contact Center kept it before its hours moved to the Business Hours module. It is
/// read only to move stored calendars, and the calendars of older <c>ContactCenterBusinessHoursCalendar</c> recipe steps,
/// to Business Hours schedules.
/// </summary>
internal sealed class LegacyBusinessHoursCalendar : CatalogItem
{
    public string Name { get; set; }

    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the time zone; empty means UTC.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets one open window per day of the week; a day that is not open is closed.
    /// </summary>
    public IList<LegacyBusinessHoursDay> WeeklySchedule { get; set; } = [];

    /// <summary>
    /// Gets or sets the dates closed all day.
    /// </summary>
    public IList<DateOnly> Holidays { get; set; } = [];

    public bool Enabled { get; set; } = true;

    public DateTime CreatedUtc { get; set; }

    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// The open window of one day of a <see cref="LegacyBusinessHoursCalendar"/>, in minutes from local midnight.
/// </summary>
internal sealed class LegacyBusinessHoursDay
{
    public DayOfWeek Day { get; set; }

    public bool IsOpen { get; set; }

    public int OpenMinute { get; set; }

    public int CloseMinute { get; set; }
}
