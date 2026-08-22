using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Orchestrates transfers of live interactions to another agent, queue, or external destination. It owns
/// the transfer decision and the interaction history; provider modules execute the media handoff.
/// </summary>
public interface IContactCenterTransferService
{
    /// <summary>
    /// Transfers a live interaction to the requested destination, records the transfer on the
    /// interaction history, and — for queue transfers — re-enqueues the underlying activity for routing.
    /// </summary>
    /// <param name="request">The transfer request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The transfer result.</returns>
    Task<TransferResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default);
}
