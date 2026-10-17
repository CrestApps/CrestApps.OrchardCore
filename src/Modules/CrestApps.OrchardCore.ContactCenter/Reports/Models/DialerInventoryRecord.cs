using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// One pending campaign record as the dialer queue inventory classified it.
/// </summary>
public sealed class DialerInventoryRecord
{
    /// <summary>
    /// Gets or sets the activity identifier.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the campaign identifier.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the record has an item in its campaign's queue.
    /// </summary>
    public bool Queued { get; set; }

    /// <summary>
    /// Gets or sets the dialer profile stamped on the record's queue item, or <see langword="null"/> when it is not queued
    /// or its item carries none.
    /// </summary>
    public string StampedProfileId { get; set; }

    /// <summary>
    /// Gets or sets the stamped profile's name, or <see langword="null"/> when there is none, it is the built-in
    /// queued-callback profile, or it no longer exists.
    /// </summary>
    public string StampedProfileName { get; set; }

    /// <summary>
    /// Gets or sets what holds the record back, <see cref="DispatchBlocker.DialableNow"/> when nothing does, or
    /// <see langword="null"/> when the record was not explained.
    /// </summary>
    public DispatchBlocker? Blocker { get; set; }

    /// <summary>
    /// Gets or sets where the record stands with the dialer, or <see langword="null"/> when it was not explained.
    /// </summary>
    public DialerQueueState? State { get; set; }

    /// <summary>
    /// Gets or sets when the record becomes due, when it waits for a time set on it or for the retry delay to end;
    /// otherwise <see langword="null"/>.
    /// </summary>
    public DateTime? DueUtc { get; set; }
}
