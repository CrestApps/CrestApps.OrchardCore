using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.RoutingRules;

/// <summary>
/// Finds the agents a routing rule can give a conversation to, from the place the rule names: a queue, teams, roles or
/// named users. A user is only a candidate with an agent profile, because a conversation is assigned to an agent.
/// </summary>
internal sealed class MessagingRoutingCandidateResolver
{
    /// <summary>
    /// The most users a rule's teams, roles or named users are read for, so a rule on a very large role stays cheap on
    /// the inbound-message path.
    /// </summary>
    public const int MaxCandidateUsers = 500;

    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly ITeamMembershipService _teamMembershipService;
    private readonly UserManager<IUser> _userManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRoutingCandidateResolver"/> class.
    /// </summary>
    /// <param name="agentProfileManager">The agent profiles.</param>
    /// <param name="queueManager">The queues, read for their required skills.</param>
    /// <param name="teamMembershipService">The team memberships.</param>
    /// <param name="userManager">The users, read for the users in a role.</param>
    /// <param name="clock">The clock, read for the day team memberships must count on.</param>
    public MessagingRoutingCandidateResolver(
        IAgentProfileManager agentProfileManager,
        IActivityQueueManager queueManager,
        ITeamMembershipService teamMembershipService,
        UserManager<IUser> userManager,
        IClock clock)
    {
        _agentProfileManager = agentProfileManager;
        _queueManager = queueManager;
        _teamMembershipService = teamMembershipService;
        _userManager = userManager;
        _clock = clock;
    }

    /// <summary>
    /// Finds a rule's candidates for a conversation.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="conversationQueueId">The queue the conversation belongs to, used when the rule names no queue of its own.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The candidates, and the skills a queue source requires of them.</returns>
    public async Task<MessagingRoutingCandidates> ResolveAsync(MessagingRoutingRule rule, string conversationQueueId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        switch (rule.CandidateSource)
        {
            case MessagingRoutingCandidateSources.QueueSignedIn:
            case MessagingRoutingCandidateSources.QueueMembers:
                {
                    var queueId = string.IsNullOrWhiteSpace(rule.CandidateQueueId) ? conversationQueueId : rule.CandidateQueueId.Trim();

                    if (string.IsNullOrEmpty(queueId))
                    {
                        return MessagingRoutingCandidates.None;
                    }

                    var agents = rule.CandidateSource == MessagingRoutingCandidateSources.QueueSignedIn
                        ? await _agentProfileManager.GetSignedInForQueueAsync(queueId, cancellationToken)
                        : await _agentProfileManager.GetMembersForQueueAsync(queueId, cancellationToken);

                    // The queue's required skills are the same ones voice routing honors, so a skill is defined once.
                    var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);

                    return new MessagingRoutingCandidates(agents.ToArray(), SkillTag.CreateAll(queue?.RequiredSkills ?? []));
                }

            case MessagingRoutingCandidateSources.Teams:
                {
                    var today = DateOnly.FromDateTime(_clock.UtcNow);
                    var userIds = new List<string>();

                    foreach (var teamId in Distinct(rule.TeamIds))
                    {
                        foreach (var member in await _teamMembershipService.GetMembersAsync(teamId, includeSubTeams: true, on: today, cancellationToken))
                        {
                            userIds.Add(member.UserId);
                        }
                    }

                    return new MessagingRoutingCandidates(await FindAgentsAsync(userIds, cancellationToken), []);
                }

            case MessagingRoutingCandidateSources.Roles:
                {
                    var userIds = new List<string>();

                    foreach (var role in Distinct(rule.RoleNames))
                    {
                        foreach (var user in await _userManager.GetUsersInRoleAsync(role))
                        {
                            if (await _userManager.GetUserIdAsync(user) is { Length: > 0 } id)
                            {
                                userIds.Add(id);
                            }
                        }

                        if (userIds.Count >= MaxCandidateUsers)
                        {
                            break;
                        }
                    }

                    return new MessagingRoutingCandidates(await FindAgentsAsync(userIds, cancellationToken), []);
                }

            case MessagingRoutingCandidateSources.Users:
                return new MessagingRoutingCandidates(await FindAgentsAsync(rule.UserIds, cancellationToken), []);

            default:
                return MessagingRoutingCandidates.None;
        }
    }

    private async Task<IReadOnlyList<AgentProfile>> FindAgentsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        var agents = new List<AgentProfile>();

        foreach (var userId in Distinct(userIds).Take(MaxCandidateUsers))
        {
            if (await _agentProfileManager.FindByUserIdAsync(userId, cancellationToken) is { } agent)
            {
                agents.Add(agent);
            }
        }

        return agents;
    }

    private static IEnumerable<string> Distinct(IEnumerable<string> values)
        => (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// The agents a routing rule can pick from.
/// </summary>
/// <param name="Agents">The agents.</param>
/// <param name="RequiredSkills">The skills each agent must hold, normalized; empty when none are required.</param>
internal sealed record MessagingRoutingCandidates(IReadOnlyList<AgentProfile> Agents, IReadOnlyCollection<SkillTag> RequiredSkills)
{
    /// <summary>
    /// Gets an empty set of candidates.
    /// </summary>
    public static MessagingRoutingCandidates None { get; } = new([], []);
}
