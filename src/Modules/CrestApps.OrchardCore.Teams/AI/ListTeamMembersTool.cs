using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Teams.AI;

/// <summary>
/// Lists who is in a team through <see cref="ITeamMembershipService"/>, the service every module asks: the memberships
/// every member provider (such as Staff) reports for the team and, by default, its sub-teams, on one day. Names come from
/// the staff directory when Staff is on. It needs <see cref="TeamsPermissions.ManageTeams"/>, as the Teams pages do.
/// </summary>
internal sealed class ListTeamMembersTool : TeamsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listTeamMembers";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "teamId": {
          "type": "string",
          "description": "The id of the team, from listRecords with kind team."
        },
        "includeSubTeams": {
          "type": "boolean",
          "description": "Whether the members of the teams under it are listed too, as a team's managers also manage its sub-teams. Default true."
        },
        "date": {
          "type": "string",
          "description": "The day the memberships must count on, as yyyy-MM-dd. Default today. Ignored when allDates is true."
        },
        "allDates": {
          "type": "boolean",
          "description": "When true, lists every membership, past and future, each with its first and last day. Default false."
        },
        "page": {
          "type": "integer",
          "description": "The page, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many members per page, at most 50 (20 by default)."
        }
      },
      "required": ["teamId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists who is in a team on a day (today by default), with the members of its sub-teams unless includeSubTeams is false: each person's name, user id and staff member id, their role, the team or sub-team they are in, the first and last day of the membership and where it comes from. People are added to teams on their staff record (saveStaffMember).";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var teamId = ReadString(arguments, "teamId");

        if (teamId is null)
        {
            return "Give the teamId, from listRecords with kind team.";
        }

        var teamManager = services.GetRequiredService<ITeamManager>();
        var team = await teamManager.FindByIdAsync(teamId, cancellationToken);

        if (team is null)
        {
            return $"There is no team with the id '{teamId}'. Use listRecords with kind team to find it.";
        }

        var allDates = ReadBool(arguments, "allDates") ?? false;
        DateOnly? on = null;

        if (!allDates)
        {
            if (ReadString(arguments, "date") is { } dateText)
            {
                if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return "The date must be a day written as yyyy-MM-dd.";
                }

                on = date;
            }
            else
            {
                // Today in the user's or the site's time zone, the day the Teams pages and the staff editor count from.
                var now = services.GetService<ILocalClock>() is { } localClock
                    ? (await localClock.GetLocalNowAsync()).DateTime
                    : services.GetRequiredService<IClock>().UtcNow;

                on = DateOnly.FromDateTime(now);
            }
        }

        var includeSubTeams = ReadBool(arguments, "includeSubTeams") ?? true;
        var members = await services.GetRequiredService<ITeamMembershipService>().GetMembersAsync(team.ItemId, includeSubTeams, on, cancellationToken);
        var teamNames = (await teamManager.ListTreeAsync(cancellationToken))
            .Where(item => !string.IsNullOrEmpty(item.Team.ItemId))
            .GroupBy(item => item.Team.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Team.Name, StringComparer.Ordinal);
        var (staffNames, userNames) = await GetNamesAsync(services, members, cancellationToken);

        string FindName(TeamMember member)
            => !string.IsNullOrEmpty(member.StaffMemberId) && staffNames.TryGetValue(member.StaffMemberId, out var staffName)
                ? staffName
                : !string.IsNullOrEmpty(member.UserId) && userNames.TryGetValue(member.UserId, out var userName) ? userName : null;

        var ordered = members
            .Select(member => (Member: member, Name: FindName(member)))
            .OrderBy(item => item.Name ?? item.Member.UserId ?? item.Member.StaffMemberId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Member.TeamId, StringComparer.Ordinal)
            .ToList();
        var page = Math.Max(1, ReadInt(arguments, "page") ?? 1);
        var pageSize = Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize);
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new
            {
                name = item.Name,
                userId = item.Member.UserId,
                staffMemberId = item.Member.StaffMemberId,
                role = item.Member.Role,
                teamId = item.Member.TeamId,
                teamName = item.Member.TeamId is not null && teamNames.TryGetValue(item.Member.TeamId, out var teamName) ? teamName : null,
                effectiveFrom = Date(item.Member.EffectiveFrom),
                effectiveTo = Date(item.Member.EffectiveTo),
                source = item.Member.Source,
            })
            .ToList();

        return Json(new
        {
            team = new
            {
                id = team.ItemId,
                name = team.Name,
            },
            date = Date(on),
            allDates,
            includeSubTeams,
            items,
            total = ordered.Count,
            page,
            pageSize,
            truncated = (page - 1) * pageSize + items.Count < ordered.Count,
        });
    }

    // Names come from the staff record, which Staff keeps in step with the linked user; without Staff there are none.
    private static async Task<(IReadOnlyDictionary<string, string> Staff, IReadOnlyDictionary<string, string> Users)> GetNamesAsync(IServiceProvider services, IReadOnlyList<TeamMember> members, CancellationToken cancellationToken)
    {
        var empty = new Dictionary<string, string>(StringComparer.Ordinal);

        if (members.Count == 0 || services.GetService<IStaffDirectory>() is not { } directory)
        {
            return (empty, empty);
        }

        var staffIds = members
            .Select(member => member.StaffMemberId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var userIds = members
            .Where(member => string.IsNullOrEmpty(member.StaffMemberId) && !string.IsNullOrEmpty(member.UserId))
            .Select(member => member.UserId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return (
            staffIds.Length > 0 ? await directory.GetNamesAsync(staffIds, cancellationToken) : empty,
            userIds.Length > 0 ? await directory.GetNamesByUserIdAsync(userIds, cancellationToken) : empty);
    }

    private static string Date(DateOnly? date)
        => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
