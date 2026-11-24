using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Teams.Services;

/// <summary>
/// An audience rule that matches the members of some teams today, with the members of their sub-teams.
/// </summary>
internal sealed class TeamAudienceRuleSource : IUserAudienceRuleSource
{
    public const string SourceName = "Team";

    private readonly ITeamManager _teams;
    private readonly ITeamMembershipService _memberships;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public TeamAudienceRuleSource(ITeamManager teams, ITeamMembershipService memberships, ILocalClock localClock, IStringLocalizer<TeamAudienceRuleSource> stringLocalizer)
    {
        _teams = teams;
        _memberships = memberships;
        _localClock = localClock;
        S = stringLocalizer;
    }

    public string Name => SourceName;

    public LocalizedString DisplayName => S["Member of a team or its sub-teams"];

    public async Task<IReadOnlyList<UserAudienceRuleOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
        => (await _teams.GetAllAsync(cancellationToken))
            .Where(team => team.Enabled)
            .OrderBy(team => team.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(team => new UserAudienceRuleOption(team.ItemId, team.Name))
            .ToArray();

    public async Task<IReadOnlyCollection<string>> ResolveUserIdsAsync(UserAudienceRule rule, CancellationToken cancellationToken = default)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var today = await TodayAsync();

        foreach (var teamId in rule.Values)
        {
            // Only memberships that hold today: someone who left the team last month is not in its audience.
            foreach (var member in await _memberships.GetMembersAsync(teamId, includeSubTeams: true, on: today, cancellationToken: cancellationToken))
            {
                if (!string.IsNullOrEmpty(member.UserId))
                {
                    ids.Add(member.UserId);
                }
            }
        }

        return ids;
    }

    public async Task<bool> IsMatchAsync(UserAudienceRule rule, string userId, CancellationToken cancellationToken = default)
    {
        var today = await TodayAsync();

        foreach (var teamId in rule.Values)
        {
            if (await _memberships.IsMemberAsync(teamId, userId, on: today, cancellationToken: cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<DateOnly> TodayAsync()
        => DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
}
