using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of rotations.
/// </summary>
public sealed class ShiftPatternIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the rotation name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets whether the rotation is used.
    /// </summary>
    public bool Enabled { get; set; }
}
