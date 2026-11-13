using CrestApps.Core.Models;
using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.WorkOrders.Core.Legacy;

/// <summary>
/// A service level policy as Work Orders kept it before policies moved to the shared Service Levels block. Read only
/// to copy stored policies, and the <c>ServiceLevels</c> section of older recipes, to the shared policies.
/// </summary>
public sealed class LegacyServiceLevelPolicy : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the targets, by priority.
    /// </summary>
    public List<LegacyServiceLevelTarget> Targets { get; set; } = [];

    /// <summary>
    /// Gets or sets the business hours calendar the clock ran in.
    /// </summary>
    public string HoursCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the same opening hours every week the clock ran in.
    /// </summary>
    public LegacyWeeklyHours WeeklyHours { get; set; }

    /// <summary>
    /// Gets or sets the time zone of the weekly hours.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the statuses the clock stopped in.
    /// </summary>
    public List<WorkOrderStatus> PauseStatuses { get; set; } = [];

    /// <summary>
    /// Gets or sets how far through a target, in percent, the warning was raised.
    /// </summary>
    public int WarningPercent { get; set; } = 80;

    /// <summary>
    /// Gets or sets when it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
