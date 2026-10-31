using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Scheduling.Core.Indexes;

/// <summary>
/// The YesSql index of <see cref="SchedulingResource"/> documents.
/// </summary>
public sealed class SchedulingResourceIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the resource's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the staff member's user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets whether the resource takes appointments.
    /// </summary>
    public bool Enabled { get; set; }
}
