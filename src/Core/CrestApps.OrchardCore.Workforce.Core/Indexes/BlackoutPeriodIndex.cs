using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of blackout dates.
/// </summary>
public sealed class BlackoutPeriodIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the team, or empty for every team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the first day, as yyyyMMdd.
    /// </summary>
    public int StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day, as yyyyMMdd.
    /// </summary>
    public int EndDate { get; set; }

    /// <summary>
    /// Gets or sets whether it is in use.
    /// </summary>
    public bool Enabled { get; set; }
}
