using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the durable replay cursor and version for one projection. It records how far a projection has
/// been rebuilt from the source-of-truth event log so a rebuild can resume and a version bump can force a
/// full replay when the projection logic changes.
/// </summary>
public sealed class ContactCenterProjectionCheckpoint : CatalogItem
{
    /// <summary>
    /// Gets or sets the stable, versioned identifier of the projection the checkpoint belongs to.
    /// </summary>
    public string HandlerId { get; set; }

    /// <summary>
    /// Gets or sets the projection logic version the checkpoint was last rebuilt with.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the occurrence time of the last event applied during the most recent rebuild.
    /// </summary>
    public DateTime LastOccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the last event applied during the most recent rebuild, used as a
    /// stable tie-breaker when several events share the same occurrence time.
    /// </summary>
    public string LastEventId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the projection was last fully rebuilt from the event log.
    /// </summary>
    public DateTime? RebuiltUtc { get; set; }
}
