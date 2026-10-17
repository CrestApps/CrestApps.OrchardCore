using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Indexes <see cref="DialerPacingCycle"/> records by the queue and dialer profile they paced, how they ended and when.
/// </summary>
public sealed class DialerPacingCycleIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the campaign queue the cycle paced.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the dialer profile the cycle paced the queue with.
    /// </summary>
    public string DialerProfileId { get; set; }

    /// <summary>
    /// Gets or sets how the cycle ended.
    /// </summary>
    public DialerPacingCycleOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets when the latest cycle the record stands for ended.
    /// </summary>
    public DateTime CompletedUtc { get; set; }
}
