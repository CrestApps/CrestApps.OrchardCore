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
}
