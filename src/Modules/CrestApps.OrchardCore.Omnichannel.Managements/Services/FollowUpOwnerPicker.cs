using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Users;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Picks the owner of a subject action's follow-up from a pool -- a team's members, the users in roles, or a list of
/// users -- through the Teams assignment service, so activities share the one round-robin and least-loaded rule the
/// other building blocks use.
/// </summary>
public sealed class FollowUpOwnerPicker
{
    /// <summary>
    /// The work kind the activity load source answers for.
    /// </summary>
    public const string WorkKind = "OmnichannelActivity";

    private readonly ITeamAssignmentService _assignmentService;
    private readonly UserManager<IUser> _userManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FollowUpOwnerPicker"/> class.
    /// </summary>
    /// <param name="assignmentService">The Teams assignment service that picks one user.</param>
    /// <param name="userManager">The users, read for the members of roles and the picked user's name.</param>
    /// <param name="logger">The logger.</param>
    public FollowUpOwnerPicker(
        ITeamAssignmentService assignmentService,
        UserManager<IUser> userManager,
        ILogger<FollowUpOwnerPicker> logger)
    {
        _assignmentService = assignmentService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Picks one user of the pool.
    /// </summary>
    /// <param name="pool">The pool.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The user id and name, or <see langword="null"/> values when the pool has nobody to pick.</returns>
    public async Task<(string UserId, string UserName)> PickAsync(FollowUpOwnerPoolMetadata pool, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pool);

        var request = new TeamAssignmentRequest
        {
            WorkKind = WorkKind,
            Strategy = pool.Strategy == FollowUpOwnerPoolStrategy.RoundRobin ? TeamAssignmentStrategies.RoundRobin : TeamAssignmentStrategies.LeastLoaded,

            // A follow-up is worked when it is due, not now, so whoever is away today still gets their turn.
            OnlyAvailable = false,
        };

        switch (pool.Source)
        {
            case FollowUpOwnerPoolSource.Team:
                if (string.IsNullOrWhiteSpace(pool.TeamId))
                {
                    return default;
                }

                request.TeamId = pool.TeamId;
                break;

            case FollowUpOwnerPoolSource.Roles:
                var userIds = new HashSet<string>(StringComparer.Ordinal);

                foreach (var role in (pool.RoleNames ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    foreach (var user in await _userManager.GetUsersInRoleAsync(role))
                    {
                        userIds.Add(await _userManager.GetUserIdAsync(user));
                    }
                }

                request.CandidateUserIds = userIds;
                break;

            default:
                request.CandidateUserIds = (pool.UserIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
                break;
        }

        var member = await _assignmentService.SelectAsync(request, cancellationToken);

        if (member is null || string.IsNullOrEmpty(member.UserId))
        {
            return default;
        }

        var picked = await _userManager.FindByIdAsync(member.UserId);

        if (picked is null)
        {
            _logger.LogWarning("The follow-up owner pool picked user '{UserId}', who no longer exists.", member.UserId);

            return default;
        }

        return (member.UserId, await _userManager.GetUserNameAsync(picked));
    }
}

/// <summary>
/// Tells the Teams assignment service how many open activities each user owns and when each was last given one, for a
/// subject action's follow-up owner pool.
/// </summary>
internal sealed class OmnichannelActivityTeamLoadSource : ITeamMemberLoadSource
{
    // How far back an assignment counts toward the round robin. Older ones make no difference to who is next.
    private static readonly TimeSpan _roundRobinWindow = TimeSpan.FromDays(30);

    private static readonly ActivityStatus[] _openStatuses =
    [
        ActivityStatus.NotStated,
        ActivityStatus.Scheduled,
        ActivityStatus.Pending,
        ActivityStatus.AwaitingAgentResponse,
        ActivityStatus.AwaitingCustomerAnswer,
        ActivityStatus.Reserved,
        ActivityStatus.Dialing,
        ActivityStatus.InProgress,
    ];

    private readonly ISession _session;
    private readonly IClock _clock;

    public OmnichannelActivityTeamLoadSource(ISession session, IClock clock)
    {
        _session = session;
        _clock = clock;
    }

    public string WorkKind => FollowUpOwnerPicker.WorkKind;

    public async Task<IReadOnlyDictionary<string, TeamMemberLoad>> GetLoadAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        var result = new Dictionary<string, TeamMemberLoad>(StringComparer.Ordinal);

        if (ids.Length == 0)
        {
            return result;
        }

        var since = _clock.UtcNow - _roundRobinWindow;

        var open = await _session.QueryIndex<OmnichannelActivityIndex>(
                index => index.AssignedToId.IsIn(ids) && index.Status.IsIn(_openStatuses),
                collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        var recent = await _session.QueryIndex<OmnichannelActivityIndex>(
                index => index.AssignedToId.IsIn(ids) && index.AssignedToUtc >= since,
                collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        foreach (var id in ids)
        {
            result[id] = new TeamMemberLoad
            {
                Open = open.Count(index => string.Equals(index.AssignedToId, id, StringComparison.Ordinal)),
                LastAssignedUtc = recent
                    .Where(index => string.Equals(index.AssignedToId, id, StringComparison.Ordinal))
                    .Max(index => index.AssignedToUtc),
            };
        }

        return result;
    }
}
