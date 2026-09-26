using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Routes a live call an agent blind-transfers to another agent or a queue through the Contact Center's own offer
/// machinery, rather than asking the provider to move it.
/// </summary>
/// <remarks>
/// A provider cannot deliver a call to an agent or a queue by name: an agent is reached on whatever device they are
/// signed in on, and a queue is not a place at all but a list of people waiting for the next free agent. Only the
/// Contact Center knows either, so the caller stays on the platform's line, listening to hold music, while the call
/// is offered exactly as a new call would be.
/// </remarks>
public interface ITransferredCallRouter
{
    /// <summary>
    /// Releases the transferring agent and offers the caller to one named agent. When the agent does not answer,
    /// the offer's own rules apply: the caller goes to that agent's voicemail.
    /// </summary>
    /// <param name="context">The call being transferred.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<TransferResult> RouteToAgentAsync(TransferRoutingContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the transferring agent and puts the caller in a queue, offering them to its next available agent.
    /// </summary>
    /// <param name="context">The call being transferred.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<TransferResult> RouteToQueueAsync(TransferRoutingContext context, CancellationToken cancellationToken = default);
}
