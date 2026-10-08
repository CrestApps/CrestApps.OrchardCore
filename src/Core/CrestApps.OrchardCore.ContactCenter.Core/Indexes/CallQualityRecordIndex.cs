using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Indexes <see cref="Models.CallQualityRecord"/> documents by the leg they measure, the call and agent they belong
/// to, and when they were taken.
/// </summary>
public sealed class CallQualityRecordIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the key identifying one source's measurement of one leg.
    /// </summary>
    public string RecordKey { get; set; }

    /// <summary>
    /// Gets or sets where the measurement came from.
    /// </summary>
    public CallQualitySource Source { get; set; }

    /// <summary>
    /// Gets or sets how the leg rated.
    /// </summary>
    public CallQualityRating Rating { get; set; }

    /// <summary>
    /// Gets or sets the interaction the leg belongs to.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the agent on the call.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the queue the call came through.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets when the leg was measured.
    /// </summary>
    public DateTime ObservedUtc { get; set; }
}
