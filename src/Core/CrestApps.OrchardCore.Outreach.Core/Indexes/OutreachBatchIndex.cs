using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.WorkQueues;

namespace CrestApps.OrchardCore.Outreach.Core.Indexes;

/// <summary>
/// The index due outreach batches are found by.
/// </summary>
public sealed class OutreachBatchIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the run the batch belongs to.
    /// </summary>
    public string RunId { get; set; }

    /// <summary>
    /// Gets or sets the state of the batch on the work queue.
    /// </summary>
    public LeasedWorkItemStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the batch can next be claimed.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }
}
