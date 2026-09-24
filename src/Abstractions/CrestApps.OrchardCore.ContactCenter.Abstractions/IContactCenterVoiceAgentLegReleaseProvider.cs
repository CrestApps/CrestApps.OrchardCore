namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// A server-side ACD voice provider that joins an agent to a caller through an agent leg of its own, which the
/// provider does not tear down just because the caller's leg ended.
/// </summary>
/// <remarks>
/// A provider that reaches the agent's device by originating a second call and bridging it to the caller leaves that
/// leg up when the caller hangs up: the agent keeps a silent line, and the call is not over for them until their own
/// device gives up. When the Contact Center sees the call end it hands every agent leg that ended with the call, rather
/// than by hanging up itself, to this operation.
/// </remarks>
public interface IContactCenterVoiceAgentLegReleaseProvider
{
    /// <summary>
    /// Hangs up an agent leg whose call has ended. A leg that is already gone is not an error.
    /// </summary>
    /// <param name="agentLegId">The provider identifier of the agent leg the platform originated.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the provider has been asked to release the leg.</returns>
    Task ReleaseAgentLegAsync(string agentLegId, CancellationToken cancellationToken = default);
}
