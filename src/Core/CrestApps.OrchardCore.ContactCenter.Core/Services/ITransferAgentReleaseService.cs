using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Ends the transferring agent's part of a call that carries on without them.
/// </summary>
/// <remarks>
/// A transfer is the one way a call leaves an agent while it is still up, so nothing else releases them: the call's
/// own ending, which normally starts their wrap-up, happens later and is somebody else's. Their leg has to leave the
/// topology before it is hung up, or its hangup reads as the agent ending the call and takes the caller with it.
/// </remarks>
public interface ITransferAgentReleaseService
{
    /// <summary>
    /// Takes the agent off the call: their live legs end on the topology, the call and interaction stop naming
    /// them, and they move to wrap-up or straight back to ready by the same rule a call ending uses. Nothing is
    /// saved or hung up here.
    /// </summary>
    /// <param name="interaction">The interaction being transferred.</param>
    /// <param name="session">The call session, when there is one.</param>
    /// <param name="agentId">The transferring agent's profile identifier.</param>
    /// <param name="utcNow">When the agent left the call.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provider legs that carried the agent, to hang up once the change is committed.</returns>
    Task<IReadOnlyList<string>> ReleaseAsync(
        Interaction interaction,
        CallSession session,
        string agentId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hangs up the legs <see cref="ReleaseAsync"/> took off the call. A leg that is already gone is not an error.
    /// </summary>
    /// <param name="providerName">The provider that owns the legs.</param>
    /// <param name="agentLegIds">The legs to hang up.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task HangUpAsync(string providerName, IEnumerable<string> agentLegIds, CancellationToken cancellationToken = default);
}
