using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Indexes;

/// <summary>
/// Represents the YesSql index used to query projection checkpoints by handler.
/// </summary>
public sealed class ContactCenterProjectionCheckpointIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the stable, versioned identifier of the projection the checkpoint belongs to.
    /// </summary>
    public string HandlerId { get; set; }

    /// <summary>
    /// Gets or sets the projection logic version the checkpoint was last rebuilt with.
    /// </summary>
    public int Version { get; set; }
}
