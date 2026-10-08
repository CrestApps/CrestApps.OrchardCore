using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// The workspace's <see cref="IMessagingAgentNameProvider"/>. The name comes from the user behind the agent profile
/// through <see cref="IDisplayNameProvider"/> (their real full name), falling back to the agent profile's own labels
/// only when the user cannot be resolved, so the workspace never shows an agent as a raw identifier.
/// </summary>
public sealed class MessagingAgentNameProvider : IMessagingAgentNameProvider
{
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;

    public MessagingAgentNameProvider(
        IAgentProfileManager agentProfileManager,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider)
    {
        _agentProfileManager = agentProfileManager;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
    }

    /// <inheritdoc/>
    public async Task<string> GetDisplayNameAsync(string agentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(agentId))
        {
            return null;
        }

        var agent = await _agentProfileManager.FindByIdAsync(agentId, cancellationToken);

        return agent is null ? null : await GetDisplayNameAsync(agent, cancellationToken);
    }

    /// <summary>
    /// Gets the display name of an agent already loaded.
    /// </summary>
    /// <param name="agent">The agent profile.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The name, or <see langword="null"/> when the profile carries none.</returns>
    public async Task<string> GetDisplayNameAsync(AgentProfile agent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        string name = null;

        if (!string.IsNullOrEmpty(agent.UserId))
        {
            var user = await _userManager.FindByIdAsync(agent.UserId);

            if (user is not null)
            {
                name = await _displayNameProvider.GetAsync(user, cancellationToken);
            }
        }

        return !string.IsNullOrWhiteSpace(name) ? name : GetProfileLabel(agent);
    }

    // The agent profile's own labels, for when the user behind it cannot be resolved.
    internal static string GetProfileLabel(AgentProfile agent)
        => !string.IsNullOrWhiteSpace(agent.DisplayName) ? agent.DisplayName
            : !string.IsNullOrWhiteSpace(agent.UserName) ? agent.UserName
            : string.IsNullOrWhiteSpace(agent.Name) ? null : agent.Name;
}
