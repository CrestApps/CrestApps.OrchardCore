using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of published schedule changes.
/// </summary>
public sealed class ScheduleChangeIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the period.
    /// </summary>
    public string PeriodId { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets when the change was made.
    /// </summary>
    public DateTime ChangedUtc { get; set; }
}
