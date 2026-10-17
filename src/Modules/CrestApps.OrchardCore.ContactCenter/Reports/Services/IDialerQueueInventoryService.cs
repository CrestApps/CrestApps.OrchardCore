using CrestApps.OrchardCore.ContactCenter.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Reads what the dialer holds right now: every campaign queue's items grouped by the dialer profile stamped on them, and
/// every pending campaign record classified by what holds it back, using the dispatch diagnostics' own decision. The dialer
/// queue inventory and the campaign summary reports are both built on it, so they never disagree on the same records.
/// </summary>
public interface IDialerQueueInventoryService
{
    /// <summary>
    /// Reads the inventory.
    /// </summary>
    /// <param name="request">Which campaigns, and which of their pending records, to cover.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The inventory.</returns>
    Task<DialerQueueInventory> GetInventoryAsync(DialerQueueInventoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Splits classified records by campaign into what the dialer can do with them, the way
    /// <see cref="DialerQueueInventory.Campaigns"/> is split, so a report that narrows the records further still splits
    /// them the same way.
    /// </summary>
    /// <param name="records">The classified records, from <see cref="DialerQueueInventory.Records"/>.</param>
    /// <returns>One summary per campaign, by campaign identifier.</returns>
    IReadOnlyList<DialerCampaignDispatchSummary> Summarize(IEnumerable<DialerInventoryRecord> records);

    /// <summary>
    /// Reads when the newest call was placed for each of the supplied activities, from the interaction index.
    /// </summary>
    /// <param name="activityIds">The activity identifiers.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The newest call time of each activity that has had a call, by activity identifier.</returns>
    Task<IReadOnlyDictionary<string, DateTime>> GetLastAttemptsAsync(IEnumerable<string> activityIds, CancellationToken cancellationToken = default);
}
