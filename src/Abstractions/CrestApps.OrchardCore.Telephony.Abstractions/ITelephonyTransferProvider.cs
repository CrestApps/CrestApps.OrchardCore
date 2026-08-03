using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Executes the blind transfer operation a telephony provider supports. Attended transfer is a separate
/// contract, because a provider can release a call to a destination without being able to consult it first.
/// </summary>
public interface ITelephonyTransferProvider
{
    /// <summary>
    /// Transfers an active call to another destination without consulting the destination first.
    /// </summary>
    /// <param name="request">The transfer request describing the destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default);
}
