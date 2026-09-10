using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Moves an automated (AI-driven) conversation into the human lane when the AI escalates to a live agent.
/// One implementation is registered per channel; the automated conversation handlers resolve the set and
/// select the implementation that <see cref="CanHandle"/> the conversation's channel. When no implementation
/// handles the channel (for example the human destination feature is not enabled), the bot simply continues.
/// </summary>
public interface IOmnichannelHandoffService
{
    /// <summary>
    /// Determines whether this implementation handles handoffs for the specified channel.
    /// </summary>
    /// <param name="channel">The omnichannel channel (for example <c>SMS</c> or <c>Phone</c>).</param>
    /// <returns><see langword="true"/> when this implementation handles the channel; otherwise <see langword="false"/>.</returns>
    bool CanHandle(string channel);

    /// <summary>
    /// Hands the conversation described by <paramref name="request"/> off to a live agent.
    /// </summary>
    /// <param name="request">The handoff request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The handoff result.</returns>
    Task<OmnichannelHandoffResult> RequestHandoffAsync(OmnichannelHandoffRequest request, CancellationToken cancellationToken = default);
}
