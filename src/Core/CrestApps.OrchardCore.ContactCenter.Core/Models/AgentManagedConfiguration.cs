namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Carries the manager-owned, environment-portable configuration applied to an <see cref="AgentProfile"/>, without any
/// of the agent's live runtime presence state. It is the shape a deployment plan or recipe step promotes between
/// environments.
/// </summary>
public sealed class AgentManagedConfiguration
{
    /// <summary>
    /// Gets the display name shown for the agent in supervisor and queue views.
    /// </summary>
    public string DisplayName { get; init; }

    /// <summary>
    /// Gets the maximum number of concurrent voice interactions the agent can handle.
    /// </summary>
    public int MaxConcurrentInteractions { get; init; } = 1;

    /// <summary>
    /// Gets the manager-owned queue entitlements the agent is allowed to sign in to.
    /// </summary>
    public IEnumerable<string> AllowedQueueIds { get; init; }

    /// <summary>
    /// Gets the manager-owned dialer campaign entitlements the agent is allowed to sign in to.
    /// </summary>
    public IEnumerable<string> AllowedCampaignIds { get; init; }

    /// <summary>
    /// Gets the skills the agent can be routed for.
    /// </summary>
    public IEnumerable<string> Skills { get; init; }
}
