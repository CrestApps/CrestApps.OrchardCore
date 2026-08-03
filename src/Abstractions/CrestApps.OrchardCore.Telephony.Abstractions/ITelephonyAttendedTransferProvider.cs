using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Executes the attended (warm) transfer operation a telephony provider supports, where the transferring
/// party consults the destination before the call is released to it.
/// </summary>
public interface ITelephonyAttendedTransferProvider
{
    /// <summary>
    /// Starts an attended transfer of an active call to another destination.
    /// </summary>
    /// <param name="request">The transfer request describing the destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> StartAttendedTransferAsync(TransferRequest request, CancellationToken cancellationToken = default);
}
