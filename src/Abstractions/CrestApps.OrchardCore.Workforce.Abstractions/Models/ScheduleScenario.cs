using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// One draft of a schedule period. A period can hold several to compare; its primary one is published.
/// </summary>
public sealed class ScheduleScenario : CatalogItem
{
    /// <summary>
    /// Gets or sets the period.
    /// </summary>
    public string PeriodId { get; set; }

    /// <summary>
    /// Gets or sets the scenario name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets who created it.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
