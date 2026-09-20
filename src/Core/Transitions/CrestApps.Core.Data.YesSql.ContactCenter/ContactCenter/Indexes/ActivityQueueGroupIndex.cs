using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Indexes;

/// <summary>
/// Represents the YesSql index used to query queue groups.
/// </summary>
public sealed class ActivityQueueGroupIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the unique queue-group name.
    /// </summary>
    public string Name { get; set; }
}
