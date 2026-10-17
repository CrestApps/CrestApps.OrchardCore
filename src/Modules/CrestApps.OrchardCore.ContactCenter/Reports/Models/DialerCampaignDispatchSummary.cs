using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// Splits one campaign's pending records by what the dialer can do with them right now. The dialer queue inventory and the
/// campaign summary both read these totals, so the two reports agree on the same records.
/// </summary>
public sealed class DialerCampaignDispatchSummary
{
    /// <summary>
    /// Gets or sets the campaign identifier.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the number of pending records covered.
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// Gets or sets how many of them can be dialed right now.
    /// </summary>
    public int DialableNowCount { get; set; }

    /// <summary>
    /// Gets or sets how many of them are held back, for any reason, including not being in the campaign's queue.
    /// </summary>
    public int HeldBackCount { get; set; }

    /// <summary>
    /// Gets or sets how many of them were not explained, because the campaign has more pending records than one pass
    /// reads, or a record's activity could no longer be read.
    /// </summary>
    public int NotEvaluatedCount { get; set; }

    /// <summary>
    /// Gets or sets how many of them have no item in any queue at all.
    /// </summary>
    public int NotInAnyQueueCount { get; set; }

    /// <summary>
    /// Gets or sets the explained records grouped by what holds them back, with
    /// <see cref="DispatchBlocker.DialableNow"/> among them when any is dialable, largest first.
    /// </summary>
    public IList<DispatchBucket> Buckets { get; set; } = [];

    /// <summary>
    /// Gets the number of explained records held back for the supplied reason.
    /// </summary>
    /// <param name="blocker">The reason.</param>
    /// <returns>The number of records.</returns>
    public int CountOf(DispatchBlocker blocker)
    {
        return Buckets.Where(bucket => bucket.Blocker == blocker).Sum(bucket => bucket.Count);
    }
}
