namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// What the dialer holds right now: one row per campaign queue and stamped dialer profile, and each campaign's pending
/// records split by what the dialer can do with them.
/// </summary>
public sealed class DialerQueueInventory
{
    /// <summary>
    /// Gets or sets when the inventory was read.
    /// </summary>
    public DateTime GeneratedUtc { get; set; }

    /// <summary>
    /// Gets or sets the rows, by campaign, its queue's rows first and the row of records not in the queue last.
    /// </summary>
    public IList<DialerQueueInventoryRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets each covered campaign's pending records split by what the dialer can do with them.
    /// </summary>
    public IList<DialerCampaignDispatchSummary> Campaigns { get; set; } = [];
}
