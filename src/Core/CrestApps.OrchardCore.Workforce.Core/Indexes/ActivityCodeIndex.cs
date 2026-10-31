using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of activity codes.
/// </summary>
public sealed class ActivityCodeIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the activity name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what kind of activity it is.
    /// </summary>
    public ActivityKind Kind { get; set; }

    /// <summary>
    /// Gets or sets whether the code can be scheduled.
    /// </summary>
    public bool Enabled { get; set; }
}
