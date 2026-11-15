using CrestApps.Core.Models;
using CrestApps.OrchardCore.WorkQueues;

namespace CrestApps.OrchardCore.Learning.Commerce.Core.Services;

/// <summary>
/// Work on one learning purchase: grant what it bought, or take it back after a full refund or a cancellation. It is
/// queued in the unit of work that paid or refunded the order, so it exists only when that change committed.
/// </summary>
public sealed class LearningFulfillmentWork : CatalogItem, ILeasedWorkItem
{
    /// <summary>
    /// Gets or sets the purchase.
    /// </summary>
    public string PurchaseId { get; set; }

    /// <summary>
    /// Gets or sets whether the purchase's grants are taken back.
    /// </summary>
    public bool Revoke { get; set; }

    /// <summary>
    /// Gets or sets why the grants are taken back.
    /// </summary>
    public string Reason { get; set; }

    /// <inheritdoc/>
    public LeasedWorkItemStatus Status { get; set; }

    /// <inheritdoc/>
    public string OwnerToken { get; set; }

    /// <inheritdoc/>
    public long FenceToken { get; set; }

    /// <inheritdoc/>
    public int AttemptCount { get; set; }

    /// <inheritdoc/>
    public DateTime NextAttemptUtc { get; set; }

    /// <inheritdoc/>
    public string LastError { get; set; }
}
