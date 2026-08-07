using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a reusable business-hours calendar that defines when queues route work and which dates are closed.
/// </summary>
public sealed class BusinessHoursCalendar : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the unique name of the calendar.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the calendar.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the time zone the weekly schedule and holidays are evaluated in. When empty, UTC is used.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the per-day open windows that define the weekly schedule.
    /// </summary>
    public IList<BusinessHoursDay> WeeklySchedule { get; set; } = [];

    /// <summary>
    /// Gets or sets the dates the queue is closed all day regardless of the weekly schedule.
    /// </summary>
    public IList<DateOnly> Holidays { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the calendar is enabled. Disabled calendars do not gate routing.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the calendar was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the calendar was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
