using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Scheduling.Core.Indexes;

/// <summary>
/// The YesSql index of <see cref="AppointmentType"/> documents.
/// </summary>
public sealed class AppointmentTypeIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the type's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the address of its public booking page.
    /// </summary>
    public string Slug { get; set; }

    /// <summary>
    /// Gets or sets whether it can be booked.
    /// </summary>
    public bool Enabled { get; set; }
}
