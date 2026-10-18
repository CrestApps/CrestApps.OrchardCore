using CrestApps.OrchardCore.Omnichannel.Core.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// Says which campaigns, and which of their pending records, a dialer queue inventory covers.
/// </summary>
public sealed class DialerQueueInventoryRequest
{
    /// <summary>
    /// Gets or sets the campaigns to cover, or <see langword="null"/> for every campaign.
    /// </summary>
    public IReadOnlyCollection<string> CampaignIds { get; set; }

    /// <summary>
    /// Gets or sets the pending campaign activities to classify, when the caller has already read them, such as a report
    /// that narrows them by period and filters. When it is <see langword="null"/>, every pending activity of the covered
    /// campaigns is read from the activity index. Activities that are not pending, or belong to no campaign, are ignored.
    /// </summary>
    public IReadOnlyCollection<OmnichannelActivityIndex> PendingActivities { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the inventory's rows are built: the queue rows, with each queue's pacing
    /// and last call. A report that only needs the records and the campaign split turns it off, which saves reading each
    /// campaign queue's head item and last call. It is on by default.
    /// </summary>
    public bool IncludeQueueRows { get; set; } = true;
}
