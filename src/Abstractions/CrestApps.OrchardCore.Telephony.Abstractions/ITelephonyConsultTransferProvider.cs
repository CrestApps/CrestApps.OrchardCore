using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Follows and finishes a transfer the provider started as a call of its own: a warm transfer's consult, or a blind
/// transfer that rings its destination before the call is handed over.
/// </summary>
/// <remarks>
/// Each result's call is the call being transferred, and carries where the transfer stands in
/// <see cref="TelephonyConstants.CallMetadata.ConsultStatus"/> (with <see cref="TelephonyConstants.CallMetadata.ConsultId"/>,
/// <see cref="TelephonyConstants.CallMetadata.ConsultLive"/> and
/// <see cref="TelephonyConstants.CallMetadata.ConsultCallEnded"/>).
/// </remarks>
public interface ITelephonyConsultTransferProvider
{
    /// <summary>
    /// Reports where a transfer stands.
    /// </summary>
    /// <param name="request">The call and its transfer's leg.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> GetConsultAsync(ConsultTransferRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hands the call to the destination the agent consulted, and releases the agent.
    /// </summary>
    /// <param name="request">The call and its consult's leg.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> CompleteConsultAsync(ConsultTransferRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls the transfer off: the destination is released and the call stays with the agent.
    /// </summary>
    /// <param name="request">The call and its transfer's leg.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> CancelConsultAsync(ConsultTransferRequest request, CancellationToken cancellationToken = default);
}
