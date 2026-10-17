using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// One campaign queue and one dialer profile stamped on its items, or the pending records of a campaign that are not in
/// its queue at all.
/// </summary>
public sealed class DialerQueueInventoryRow
{
    /// <summary>
    /// Gets or sets the campaign identifier.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the campaign's queue identifier, or <see langword="null"/> for the row that holds the campaign's
    /// pending records that are not in its queue.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the dialer profile stamped on the row's queue items, or <see langword="null"/> when they carry none or
    /// the row holds records that are not queued.
    /// </summary>
    public string StampedProfileId { get; set; }

    /// <summary>
    /// Gets or sets the stamped profile's name, or <see langword="null"/> when it cannot be read.
    /// </summary>
    public string StampedProfileName { get; set; }

    /// <summary>
    /// Gets or sets the stamped profile's dialing mode name, or <see langword="null"/> when it cannot be read.
    /// </summary>
    public string StampedProfileMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the stamped profile is turned on, or <see langword="null"/> when it cannot be
    /// read.
    /// </summary>
    public bool? StampedProfileEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the stamped profile is the built-in queued-callback profile.
    /// </summary>
    public bool StampedProfileIsCallback { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the items are stamped with a profile that no longer exists.
    /// </summary>
    public bool StampedProfileMissing { get; set; }

    /// <summary>
    /// Gets or sets the number of the row's queue items waiting to be dialed or offered.
    /// </summary>
    public int WaitingCount { get; set; }

    /// <summary>
    /// Gets or sets the number of the row's queue items reserved for an agent.
    /// </summary>
    public int ReservedCount { get; set; }

    /// <summary>
    /// Gets or sets the number of the row's queue items assigned to an agent.
    /// </summary>
    public int AssignedCount { get; set; }

    /// <summary>
    /// Gets or sets when the row's oldest waiting item entered the queue, or <see langword="null"/> when none waits.
    /// </summary>
    public DateTime? OldestWaitingUtc { get; set; }

    /// <summary>
    /// Gets or sets the dialer profiles the row's pending activities carry themselves, most first.
    /// </summary>
    public IList<DialerInventoryProfileCount> ActivityProfiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the row's pending activities grouped by source, interaction type and channel, largest first.
    /// </summary>
    public IList<DispatchActivityGroup> ActivityMix { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of the row's pending activities.
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// Gets or sets how many of them can be dialed right now.
    /// </summary>
    public int DialableNowCount { get; set; }

    /// <summary>
    /// Gets or sets how many of them are held back, for any reason.
    /// </summary>
    public int HeldBackCount { get; set; }

    /// <summary>
    /// Gets or sets how many of them were not explained.
    /// </summary>
    public int NotEvaluatedCount { get; set; }

    /// <summary>
    /// Gets or sets the held-back records grouped by reason, largest first.
    /// </summary>
    public IList<DispatchBucket> HeldBackReasons { get; set; } = [];

    /// <summary>
    /// Gets or sets how many of the row's pending activities are stamped on their queue item with a different dialer
    /// profile than the one the activity carries. The dialer trusts the stamp.
    /// </summary>
    public int ProfileMismatchCount { get; set; }

    /// <summary>
    /// Gets or sets how many of the row's pending activities have no item in any queue.
    /// </summary>
    public int NotInAnyQueueCount { get; set; }

    /// <summary>
    /// Gets or sets how the row's queue is paced, from the profile stamped on the item at its head, or
    /// <see langword="null"/> for the row of records that are not queued.
    /// </summary>
    public DispatchPacing? Pacing { get; set; }

    /// <summary>
    /// Gets or sets when a call was last placed from the row's queue, or <see langword="null"/> when none was.
    /// </summary>
    public DateTime? LastDialedUtc { get; set; }
}
