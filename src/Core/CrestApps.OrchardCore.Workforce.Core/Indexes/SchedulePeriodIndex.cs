using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of schedule periods.
/// </summary>
public sealed class SchedulePeriodIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the first day as <c>yyyyMMdd</c>.
    /// </summary>
    public int StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day as <c>yyyyMMdd</c>.
    /// </summary>
    public int EndDate { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public SchedulePeriodStatus Status { get; set; }
}
