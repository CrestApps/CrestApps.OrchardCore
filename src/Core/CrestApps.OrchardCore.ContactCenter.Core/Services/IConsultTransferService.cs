using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Runs an attended (warm) transfer as the three phases it actually is: consult, then either complete or cancel.
/// </summary>
public interface IConsultTransferService
{
    /// <summary>
    /// Places a private consult, holding the customer while the agent speaks to the destination.
    /// </summary>
    /// <param name="request">The consult request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The recorded consult, or <see langword="null"/> when it could not be started.</returns>
    Task<ConsultCall> StartAsync(ConsultTransferRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the destination answered and the two are now talking.
    /// </summary>
    /// <param name="callSessionId">The call the customer is on.</param>
    /// <param name="consultId">The consult.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<bool> MarkConnectedAsync(string callSessionId, string consultId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the handover: the customer is joined to the destination and the agent leaves.
    /// </summary>
    /// <param name="callSessionId">The call the customer is on.</param>
    /// <param name="consultId">The consult.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<bool> CompleteAsync(string callSessionId, string consultId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Abandons the handover: the destination is dropped and the customer returns to the agent they had.
    /// </summary>
    /// <param name="callSessionId">The call the customer is on.</param>
    /// <param name="consultId">The consult.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<bool> CancelAsync(string callSessionId, string consultId, CancellationToken cancellationToken = default);
}
