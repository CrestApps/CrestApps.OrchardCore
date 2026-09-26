namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Resolves the name the workspace shows for an agent: the person behind the agent profile, never its identifier.
/// </summary>
public interface IMessagingAgentNameProvider
{
    /// <summary>
    /// Gets the display name of an agent.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The name, or <see langword="null"/> when the agent cannot be resolved.</returns>
    Task<string> GetDisplayNameAsync(string agentId, CancellationToken cancellationToken = default);
}
