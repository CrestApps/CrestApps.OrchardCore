using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of labor rule overrides.
/// </summary>
public sealed class LaborRuleOverrideIndex : CatalogItemIndex
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
    /// Gets or sets the UTC time it was recorded.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
