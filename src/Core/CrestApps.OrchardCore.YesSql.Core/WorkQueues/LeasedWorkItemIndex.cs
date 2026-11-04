using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.WorkQueues;

namespace CrestApps.OrchardCore.YesSql.Core.WorkQueues;

/// <summary>
/// The index a <see cref="LeasedWorkQueue{T}"/> finds due items by. One table per collection serves every work item
/// type stored in it.
/// </summary>
public sealed class LeasedWorkItemIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the state of the item.
    /// </summary>
    public LeasedWorkItemStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the item can next be claimed: its retry time while pending, its lease expiry while claimed.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }
}
