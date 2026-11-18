using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.BusinessHours.Models;

/// <summary>
/// A business hours schedule kept by the Business Hours module: a name, the weekly hours and the dates that differ. It
/// is one source of the schedules <see cref="Services.IOpenHoursService"/> reads, so queues, service levels and any
/// other module that asks "are we open?" can use it.
/// </summary>
public sealed class BusinessHoursSchedule : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the name, unique among the schedules.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a description for administrators.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether the schedule is in use. A disabled schedule is unknown to the open-hours service, so the
    /// records that use it count around the clock.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the hours: the time zone, the weekly windows and the date exceptions.
    /// </summary>
    public HoursSchedule Hours { get; set; } = new();

    /// <summary>
    /// Gets or sets when the schedule was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the schedule was last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
