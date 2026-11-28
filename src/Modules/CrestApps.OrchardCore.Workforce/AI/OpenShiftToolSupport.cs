using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Controllers;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Shared reading and result shapes of the open shift and shift trade AI tools.
/// </summary>
internal static class OpenShiftToolSupport
{
    /// <summary>
    /// How far ahead a worker's shifts are offered for a trade, as on the Shift Trades page.
    /// </summary>
    public const int TradeDays = 60;

    /// <summary>
    /// Finds one team by its ID or name among the teams the user manages.
    /// </summary>
    public static async Task<(Team Team, string Error)> FindManagedTeamAsync(IServiceProvider services, ClaimsPrincipal user, string teamName, CancellationToken cancellationToken)
    {
        if (teamName is null)
        {
            return (null, "Give the team (teamName), by its name or ID.");
        }

        var team = (await services.GetRequiredService<ITeamManager>().ListTreeAsync(cancellationToken))
            .Select(entry => entry.Team)
            .FirstOrDefault(item => item.ItemId == teamName || WorkforceNames.Normalize(item.Name) == WorkforceNames.Normalize(teamName));

        if (team is null)
        {
            return (null, $"No team is called '{teamName}'.");
        }

        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);

        return scope.Covers(team.ItemId) ? (team, null) : (null, $"The user doesn't manage {team.Name}.");
    }

    /// <summary>
    /// Finds an open shift of a team the user manages.
    /// </summary>
    public static async Task<(OpenShift Shift, string Error)> FindManagedShiftAsync(IServiceProvider services, ClaimsPrincipal user, string id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return (null, "Give the open shift's ID (openShiftId), from listOpenShifts.");
        }

        var shift = await services.GetRequiredService<IOpenShiftService>().FindAsync(id, cancellationToken);

        if (shift is null)
        {
            return (null, $"No open shift has the ID '{id}'.");
        }

        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);

        return scope.Covers(shift.TeamId) ? (shift, null) : (null, "The user doesn't manage this open shift's team.");
    }

    /// <summary>
    /// Finds a shift trade of a team the user manages.
    /// </summary>
    public static async Task<(ShiftTrade Trade, string Error)> FindManagedTradeAsync(IServiceProvider services, ClaimsPrincipal user, string id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return (null, "Give the trade's ID (tradeId), from listShiftTrades.");
        }

        var trade = await services.GetRequiredService<IShiftTradeService>().FindAsync(id, cancellationToken);

        if (trade is null)
        {
            return (null, $"No shift trade has the ID '{id}'.");
        }

        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);

        return scope.Covers(trade.TeamId) ? (trade, null) : (null, "The user doesn't manage this trade's team.");
    }

    /// <summary>
    /// Copies the target worker's arguments under the names the worker lookup reads.
    /// </summary>
    public static AIFunctionArguments TargetArguments(AIFunctionArguments arguments)
        => new(new Dictionary<string, object>
        {
            ["workerId"] = WorkforceAdminTools.Read(arguments, "targetWorkerId"),
            ["workerName"] = WorkforceAdminTools.Read(arguments, "targetWorkerName"),
        });

    /// <summary>
    /// Describes an open shift. Its day and times are the team's own wall-clock times, as on the Open Shifts page.
    /// </summary>
    public static async Task<object> DescribeShiftAsync(IServiceProvider services, OpenShift shift, bool withClaims, CancellationToken cancellationToken)
    {
        var teams = services.GetRequiredService<ITeamManager>();
        var workers = services.GetRequiredService<IStaffMemberManager>();
        var skills = services.GetRequiredService<ISkillManager>();
        var skillNames = new List<string>();

        foreach (var skillId in shift.RequiredSkillIds)
        {
            skillNames.Add((await skills.FindByIdAsync(skillId, cancellationToken))?.Name ?? skillId);
        }

        List<object> claims = null;

        if (withClaims)
        {
            claims = [];

            foreach (var claim in shift.Claims.OrderByDescending(claim => claim.ClaimedUtc))
            {
                claims.Add(new
                {
                    workerId = claim.WorkerId,
                    worker = (await workers.FindByIdAsync(claim.WorkerId, cancellationToken))?.DisplayName ?? claim.WorkerId,
                    status = claim.Status.ToString(),
                    claimed = WorkforceAdminTools.InZone(claim.ClaimedUtc, shift.TimeZoneId),
                    note = claim.Note,
                });
            }
        }

        return new
        {
            id = shift.ItemId,
            teamId = shift.TeamId,
            team = (await teams.FindByIdAsync(shift.TeamId, cancellationToken))?.Name,
            date = WorkforceAdminTools.Day(shift.Date),
            start = WallClock(shift.Start),
            end = WallClock(shift.End),
            timeZone = shift.TimeZoneId,
            status = shift.Status.ToString(),
            claimPolicy = shift.ClaimPolicy.ToString(),
            claimDeadline = WorkforceAdminTools.InZone(shift.ClaimDeadlineUtc, shift.TimeZoneId),
            requiredSkills = skillNames,
            notes = shift.Notes,
            filledByWorkerId = shift.FilledWorkerId,
            filledBy = string.IsNullOrEmpty(shift.FilledWorkerId) ? null : (await workers.FindByIdAsync(shift.FilledWorkerId, cancellationToken))?.DisplayName,
            claims,
        };
    }

    /// <summary>
    /// Describes a shift trade, with its shifts in the team's time zone, as on the Shift Trades page.
    /// </summary>
    public static async Task<object> DescribeTradeAsync(IServiceProvider services, ShiftTrade trade, IReadOnlyList<string> problems, CancellationToken cancellationToken)
    {
        var team = await services.GetRequiredService<ITeamManager>().FindByIdAsync(trade.TeamId, cancellationToken);
        var zone = await PlanningOptions.TimeZoneOfAsync(team, services.GetRequiredService<ISiteService>());
        var workers = services.GetRequiredService<IStaffMemberManager>();

        async Task<string> NameAsync(string workerId)
            => string.IsNullOrEmpty(workerId) ? null : (await workers.FindByIdAsync(workerId, cancellationToken))?.DisplayName ?? workerId;

        return new
        {
            id = trade.ItemId,
            teamId = trade.TeamId,
            team = team?.Name,
            kind = trade.Kind.ToString(),
            status = trade.Status.ToString(),
            offeredByWorkerId = trade.OfferedByWorkerId,
            offeredBy = await NameAsync(trade.OfferedByWorkerId),
            shiftStart = WorkforceAdminTools.InZone(trade.ShiftStartUtc, zone),
            shiftEnd = WorkforceAdminTools.InZone(trade.ShiftEndUtc, zone),
            targetWorkerId = trade.TargetWorkerId,
            target = trade.TargetWorkerId is null ? "anyone on the team" : await NameAsync(trade.TargetWorkerId),
            targetShiftStart = WorkforceAdminTools.InZone(trade.TargetShiftStartUtc, zone),
            targetShiftEnd = WorkforceAdminTools.InZone(trade.TargetShiftEndUtc, zone),
            acceptedBy = await NameAsync(trade.AcceptedByWorkerId),
            requiresApproval = trade.RequiresApproval,
            note = trade.Note,
            decisionNote = trade.DecisionNote,
            decidedBy = trade.DecidedBy,
            created = WorkforceAdminTools.InZone(trade.CreatedUtc, zone),
            timeZone = zone,
            problems,
        };
    }

    /// <summary>
    /// Formats a stored wall-clock time as it was entered.
    /// </summary>
    public static string WallClock(DateTime value)
        => value == default ? null : value.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a calendar shift's local start or end.
    /// </summary>
    public static string ShiftTime(DateTime utc, string timeZoneId)
        => CalendarTime.ToLocal(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZoneId ?? "UTC").ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
}
