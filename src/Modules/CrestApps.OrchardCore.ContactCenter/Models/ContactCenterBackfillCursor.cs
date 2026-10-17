namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents the position of the last event a backfill has read, in the order the event log is read: by occurrence
/// time, then by document.
/// </summary>
public sealed class ContactCenterBackfillCursor
{
    /// <summary>
    /// Gets or sets the UTC occurrence time of the last event read.
    /// </summary>
    public DateTime OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the document identifier of the last event read.
    /// </summary>
    public long DocumentId { get; set; }
}
