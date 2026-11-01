using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of time-off types.
/// </summary>
public sealed class TimeOffTypeIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the type name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets whether the type can be used.
    /// </summary>
    public bool Enabled { get; set; }
}
