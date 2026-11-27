using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Services;

/// <summary>
/// Fills in what a Contact Center event's payload and actor did not say about the agent it is about, for the
/// workflow input.
/// </summary>
public interface IContactCenterEventAgentResolver
{
    /// <summary>
    /// Completes the agent an event is about: the agent the call was placed or connected for, that agent's user, and the
    /// user's name.
    /// </summary>
    /// <param name="interactionEvent">The event.</param>
    /// <param name="agentId">The agent profile the event's payload names, if any.</param>
    /// <param name="agentUserId">The agent's user the event's payload or actor names, if any.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent profile, its user and the user's name; each <see langword="null"/> when unknown.</returns>
    Task<(string AgentId, string AgentUserId, string AgentUserName)> ResolveAsync(
        InteractionEvent interactionEvent,
        string agentId,
        string agentUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves the agent from the event and the call, without agent profiles: the user name of a user the event names,
/// and the agent profile a call records. Used while the Contact Center Agent Services feature is off.
/// </summary>
internal class ContactCenterEventAgentResolver : IContactCenterEventAgentResolver
{
    private readonly IInteractionManager _interactionManager;
    private readonly UserManager<IUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEventAgentResolver"/> class.
    /// </summary>
    /// <param name="interactionManager">The interactions, read for the agent a call was placed or connected for.</param>
    /// <param name="userManager">The users, read for the user name of the agent.</param>
    public ContactCenterEventAgentResolver(IInteractionManager interactionManager, UserManager<IUser> userManager)
    {
        _interactionManager = interactionManager;
        _userManager = userManager;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The dialer reserves an agent before a Power or Progressive call and records them on the call, and a Predictive
    /// call records the agent it connects once a person answers. Most dialer events carry no agent of their own, so a
    /// workflow that wanted to give the follow-up to the agent the dialer called for had nothing to read. A Predictive
    /// call nobody answered never had an agent, so it names none.
    /// </remarks>
    public async Task<(string AgentId, string AgentUserId, string AgentUserName)> ResolveAsync(
        InteractionEvent interactionEvent,
        string agentId,
        string agentUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (string.IsNullOrEmpty(agentId) &&
            string.IsNullOrEmpty(agentUserId) &&
            !string.IsNullOrEmpty(interactionEvent.InteractionId))
        {
            var interaction = await _interactionManager.FindByIdAsync(interactionEvent.InteractionId, cancellationToken);

            agentId = interaction?.AgentId;
        }

        if (string.IsNullOrEmpty(agentUserId) && !string.IsNullOrEmpty(agentId))
        {
            agentUserId = await FindUserIdAsync(agentId, cancellationToken);
        }

        string agentUserName = null;

        if (!string.IsNullOrEmpty(agentUserId))
        {
            var user = await _userManager.FindByIdAsync(agentUserId);

            agentUserName = user is null ? null : await _userManager.GetUserNameAsync(user);
        }

        return (agentId, agentUserId, agentUserName);
    }

    /// <summary>
    /// Finds the user of an agent profile. Without agent profiles there is none to read.
    /// </summary>
    /// <param name="agentId">The agent profile.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The user id, or <see langword="null"/>.</returns>
    protected virtual Task<string> FindUserIdAsync(string agentId, CancellationToken cancellationToken)
        => Task.FromResult<string>(null);
}

/// <summary>
/// Resolves the agent from the event, the call and the agent profiles, so an event that names only an agent profile
/// also names its user. Used when the Contact Center Agent Services feature is on.
/// </summary>
internal sealed class AgentProfileEventAgentResolver : ContactCenterEventAgentResolver
{
    private readonly IAgentProfileManager _agentProfileManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentProfileEventAgentResolver"/> class.
    /// </summary>
    /// <param name="interactionManager">The interactions, read for the agent a call was placed or connected for.</param>
    /// <param name="agentProfileManager">The agent profiles, read for the user of the agent.</param>
    /// <param name="userManager">The users, read for the user name of the agent.</param>
    public AgentProfileEventAgentResolver(
        IInteractionManager interactionManager,
        IAgentProfileManager agentProfileManager,
        UserManager<IUser> userManager)
        : base(interactionManager, userManager)
    {
        _agentProfileManager = agentProfileManager;
    }

    /// <inheritdoc/>
    protected override async Task<string> FindUserIdAsync(string agentId, CancellationToken cancellationToken)
        => (await _agentProfileManager.FindByIdAsync(agentId, cancellationToken))?.UserId;
}
