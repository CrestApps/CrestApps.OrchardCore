using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of predictability pay entries.
/// </summary>
public sealed class PredictabilityPayEntryIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the change that caused it.
    /// </summary>
    public string ScheduleChangeId { get; set; }

    /// <summary>
    /// Gets or sets where it stands.
    /// </summary>
    public PredictabilityPayStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was recorded.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
