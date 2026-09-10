using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Executes provider-confirmed live-call transfers.
/// </summary>
public interface IContactCenterVoiceTransferProvider
{
    /// <summary>
    /// Transfers a live provider call.
    /// </summary>
    /// <param name="request">The transfer request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> TransferAsync(
        ContactCenterVoiceTransferRequest request,
        CancellationToken cancellationToken = default);
}
