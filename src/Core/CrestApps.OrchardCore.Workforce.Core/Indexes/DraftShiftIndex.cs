using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of draft shifts.
/// </summary>
public sealed class DraftShiftIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the scenario.
    /// </summary>
    public string ScenarioId { get; set; }

    /// <summary>
    /// Gets or sets the period.
    /// </summary>
    public string PeriodId { get; set; }

    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the day as <c>yyyyMMdd</c>.
    /// </summary>
    public int Date { get; set; }
}
