using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of time-off allotments.
/// </summary>
public sealed class TimeOffAllotmentIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the allotment name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets whether the allotment is in use.
    /// </summary>
    public bool Enabled { get; set; }
}
