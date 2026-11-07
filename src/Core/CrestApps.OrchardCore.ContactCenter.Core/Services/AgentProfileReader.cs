using CrestApps.OrchardCore.ContactCenter.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IAgentProfileReader"/>, backed by the agent directory.
/// </summary>
public sealed class AgentProfileReader : IAgentProfileReader
{
    private readonly IAgentProfileManager _agentProfileManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentProfileReader"/> class.
    /// </summary>
    /// <param name="agentProfileManager">The agent directory.</param>
    public AgentProfileReader(IAgentProfileManager agentProfileManager)
    {
        _agentProfileManager = agentProfileManager;
    }

    /// <inheritdoc/>
    public async Task<string> FindAgentIdByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var profile = await _agentProfileManager.FindByUserIdAsync(userId, cancellationToken);

        return profile?.ItemId;
    }
}
