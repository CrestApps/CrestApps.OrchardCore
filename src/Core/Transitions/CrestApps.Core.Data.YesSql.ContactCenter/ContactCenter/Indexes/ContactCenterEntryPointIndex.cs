using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Indexes;

/// <summary>
/// Represents the YesSql index used to query inbound entry points.
/// </summary>
public sealed class ContactCenterEntryPointIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the unique entry point name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry point is enabled.
    /// </summary>
    public bool Enabled { get; set; }
}
