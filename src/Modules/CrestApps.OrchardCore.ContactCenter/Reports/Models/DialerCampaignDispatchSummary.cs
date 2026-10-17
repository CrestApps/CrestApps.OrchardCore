using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// Splits one campaign's pending records by what the dialer can do with them right now. The dialer queue inventory and the
/// campaign summary both read these totals, computed by the same code from the same classification, so the two reports
/// agree on the same records.
/// </summary>
/// <remarks>
/// <see cref="DialableNowCount"/>, <see cref="ScheduledForLaterCount"/>, <see cref="NotInAnyQueueCount"/>,
/// <see cref="AssignedToUserCount"/>, <see cref="OtherHeldBackCount"/> and <see cref="NotEvaluatedCount"/> add up to
/// <see cref="PendingCount"/>.
/// </remarks>
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
    /// Gets or sets how many of them are held back, for any reason: scheduled for later, in no queue, assigned to a user,
    /// or any other.
    /// </summary>
    public int HeldBackCount { get; set; }

    /// <summary>
    /// Gets or sets how many of them wait for a time set on them.
    /// </summary>
    public int ScheduledForLaterCount { get; set; }

    /// <summary>
    /// Gets or sets how many of them have no item in any queue and are not assigned to a user.
    /// </summary>
    public int NotInAnyQueueCount { get; set; }

    /// <summary>
    /// Gets or sets how many of them are assigned to a user for manual work.
    /// </summary>
    public int AssignedToUserCount { get; set; }

    /// <summary>
    /// Gets the number held back for a reason other than being scheduled for later, in no queue or assigned to a user.
    /// </summary>
    public int OtherHeldBackCount => HeldBackCount - ScheduledForLaterCount - NotInAnyQueueCount - AssignedToUserCount;

    /// <summary>
    /// Gets or sets how many of them were not explained, because the campaign has more queued pending records than one
    /// pass reads, or a record's activity could no longer be read.
    /// </summary>
    public int NotEvaluatedCount { get; set; }

    /// <summary>
    /// Gets or sets the explained records grouped by what holds them back, with
    /// <see cref="DispatchBlocker.DialableNow"/> among them when any is dialable, largest first.
    /// </summary>
    public IList<DispatchBucket> Buckets { get; set; } = [];

    /// <summary>
    /// Gets or sets the records held back for a reason other than being scheduled for later, in no queue or assigned to a
    /// user, grouped by that reason, largest first. They add up to <see cref="OtherHeldBackCount"/>.
    /// </summary>
    public IList<DispatchBucket> OtherHeldBackReasons { get; set; } = [];

    /// <summary>
    /// Gets or sets when the first record that waits for a time set on it, or for the retry delay to end, becomes due, or
    /// <see langword="null"/> when none waits for a time.
    /// </summary>
    public DateTime? NextDueUtc { get; set; }

    /// <summary>
    /// Gets or sets the dialer profiles stamped on the queued pending records, with how many carry each, most first.
    /// </summary>
    public IList<DialerInventoryProfileCount> StampedProfiles { get; set; } = [];

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
