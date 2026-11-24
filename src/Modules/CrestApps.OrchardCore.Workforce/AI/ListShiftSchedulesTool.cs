using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Lists the schedules of the teams the user manages, newest first, as the Shift Schedules page does.
/// </summary>
internal sealed class ListShiftSchedulesTool : WorkforceAdminToolBase
{
    public const string ToolName = "listShiftSchedules";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "teamName": { "type": "string", "description": "Optional. A team the user manages, by name or ID; its sub-teams are included. Leave out for every team the user manages." },
        "page": { "type": "integer", "minimum": 1, "description": "The page, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Schedules per page, 20 by default, at most 50." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists the schedule periods of the teams the user manages, newest first, with team, dates, name and whether each is a draft or published. Use the ID with getShiftSchedule.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageSchedules;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (teamIds, teamError) = await WorkforceAdminTools.TeamFilterAsync(services, user, arguments, cancellationToken);

        if (teamError is not null)
        {
            return teamError;
        }

        var (page, pageSize) = WorkforceAdminTools.ReadPaging(arguments);
        var (count, periods) = await services.GetRequiredService<ISchedulePlanningService>().ListPeriodsAsync(teamIds, (page - 1) * pageSize, pageSize, cancellationToken);
        var teams = (await services.GetRequiredService<ITeamManager>().ListTreeAsync(cancellationToken)).ToDictionary(entry => entry.Team.ItemId, entry => entry.Team.Name, StringComparer.Ordinal);
        var localClock = services.GetRequiredService<ILocalClock>();
        var rows = new List<object>();

        foreach (var period in periods)
        {
            rows.Add(new
            {
                id = period.ItemId,
                name = period.Name,
                teamId = period.TeamId,
                team = teams.GetValueOrDefault(period.TeamId ?? string.Empty),
                startDate = WorkforceAdminTools.Day(period.StartDate),
                endDate = WorkforceAdminTools.Day(period.EndDate),
                weeks = ((period.EndDate.DayNumber - period.StartDate.DayNumber) / 7) + 1,
                status = period.Status.ToString(),
                publishedVersion = period.PublishedVersion,
                publishedAt = await WorkforceAdminTools.LocalAsync(localClock, period.PublishedUtc),
                publishedBy = period.PublishedBy,
                link = ShiftScheduleToolSupport.PlanPath(services, period.ItemId),
            });
        }

        return WorkforceAdminTools.Json(WorkforceAdminTools.Paged(rows, count, page, pageSize));
    }
}
