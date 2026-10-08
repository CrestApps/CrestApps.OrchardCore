using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The agent-facing side of a warm transfer: consult a colleague or an outside number while the caller is held,
/// then hand the call over or take it back.
/// </summary>
/// <remarks>
/// <see cref="IConsultTransferService"/> records the consult against the call and drives the provider. This service
/// is the part an agent's request goes through: it checks the agent is on the call, resolves the destination through
/// the same rules a blind transfer uses, and moves the call's ownership, the agents' states and the transfer history
/// along with each phase.
/// </remarks>
public interface IWarmTransferService
{
    /// <summary>
    /// Holds the caller and rings the destination privately.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The consult, or why it could not start.</returns>
    Task<WarmTransferResult> StartAsync(WarmTransferRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hands the caller to the consulted destination and takes the consulting agent off the call.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The completed consult, or why it could not complete.</returns>
    Task<WarmTransferResult> CompleteAsync(WarmTransferCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the consulted destination and returns the caller to the consulting agent.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cancelled consult, or why it could not be cancelled.</returns>
    Task<WarmTransferResult> CancelAsync(WarmTransferCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports where the agent's consult on the call stands.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The consult, or a failure when there is none.</returns>
    Task<WarmTransferResult> GetAsync(WarmTransferCommand command, CancellationToken cancellationToken = default);
}
