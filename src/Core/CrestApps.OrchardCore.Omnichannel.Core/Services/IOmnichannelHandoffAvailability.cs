using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Says whether a handoff can reach somebody now, so an automated conversation is offered a transfer only when one can
/// happen, and is told what it does when it cannot. Implemented, optionally, by an <see cref="IOmnichannelHandoffService"/>;
/// a service that does not implement it is taken to be always open.
/// </summary>
/// <remarks>
/// An assistant told it may transfer promises the customer a person. When the queue the handoff would go to is closed,
/// nobody comes: the handoff books a callback instead, or, with nothing to book it, ends the conversation with none.
/// Asked before the conversation starts, the assistant can say the right thing: that someone will call back, or that
/// nobody can take the call and it will take a message.
/// </remarks>
public interface IOmnichannelHandoffAvailability
{
    /// <summary>
    /// Whether a handoff to the queue would reach somebody now.
    /// </summary>
    /// <param name="queueId">The queue the handoff would go to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The queue's availability; an unknown queue, or one that keeps no hours, is <see cref="HandoffAvailability.Open"/>.</returns>
    Task<HandoffAvailability> GetAvailabilityAsync(string queueId, CancellationToken cancellationToken = default);
}
