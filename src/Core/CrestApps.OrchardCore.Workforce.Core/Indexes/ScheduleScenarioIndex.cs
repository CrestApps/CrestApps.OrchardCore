using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of schedule scenarios.
/// </summary>
public sealed class ScheduleScenarioIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the period.
    /// </summary>
    public string PeriodId { get; set; }
}
