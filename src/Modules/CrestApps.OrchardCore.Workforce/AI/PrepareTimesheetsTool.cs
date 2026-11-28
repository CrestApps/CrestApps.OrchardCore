using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Makes and works out the timesheets of a pay period for the workers of the teams the user manages, as the Prepare
/// button of the Timesheets page does.
/// </summary>
internal sealed class PrepareTimesheetsTool : WorkforceAdminToolBase
{
    public const string ToolName = "prepareTimesheets";

    // The Timesheets page's cap.
    private const int MaxPrepared = 500;

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "teamName": { "type": "string", "description": "Optional. A team the user manages, by name or ID; its sub-teams are included. Leave out for every team the user manages." },
        "date": { "type": "string", "description": "A day in the pay period (yyyy-MM-dd); today by default. Each worker's time clock policy decides which pay period that is." },
        "payPeriodCalendarId": { "type": "string", "description": "Optional. Count only the timesheets of these pay periods, as the page does for the pay periods it shows." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Creates the timesheets of a pay period for the workers of a team (and its sub-teams) who do not have one yet, so the period can be reviewed.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApproveTimesheets;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (teamIds, teamError) = await WorkforceAdminTools.TeamFilterAsync(services, user, arguments, cancellationToken);

        if (teamError is not null)
        {
            return teamError;
        }

        if (teamIds is null)
        {
            teamIds = (await services.GetRequiredService<ITeamManager>().ListTreeAsync(cancellationToken)).Select(entry => entry.Team.ItemId).ToArray();
        }

        var date = WorkforceAdminTools.ReadDate(arguments, "date") ?? await WorkforceAdminTools.TodayAsync(services);
        var calendarId = WorkforceAdminTools.Read(arguments, "payPeriodCalendarId");
        var workers = services.GetRequiredService<IStaffMemberManager>();
        var timesheets = services.GetRequiredService<ITimesheetService>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var periods = new Dictionary<(string CalendarId, DateOnly Start, DateOnly End), int>();
        var problems = new List<string>();
        var made = 0;
        var capped = false;

        foreach (var teamId in teamIds)
        {
            foreach (var worker in await workers.ListByTeamAsync(teamId, date, cancellationToken))
            {
                if (!seen.Add(worker.ItemId))
                {
                    continue;
                }

                if (made >= MaxPrepared)
                {
                    capped = true;

                    break;
                }

                var result = await timesheets.GetForDateAsync(worker, date, cancellationToken);

                if (!result.Succeeded)
                {
                    problems.Add($"{worker.DisplayName}: {string.Join(" ", result.Errors)}");

                    continue;
                }

                if (calendarId is not null && result.Value.PayPeriodCalendarId != calendarId)
                {
                    continue;
                }

                var key = (result.Value.PayPeriodCalendarId, result.Value.PeriodStart, result.Value.PeriodEnd);

                periods[key] = periods.GetValueOrDefault(key) + 1;
                made++;
            }
        }

        return WorkforceAdminTools.Json(new
        {
            ready = made,
            date = WorkforceAdminTools.Day(date),
            periods = periods.Select(period => new
            {
                payPeriodCalendarId = period.Key.CalendarId,
                periodStart = WorkforceAdminTools.Day(period.Key.Start),
                periodEnd = WorkforceAdminTools.Day(period.Key.End),
                timesheets = period.Value,
            }),
            notPrepared = problems.Take(20),
            notPreparedCount = problems.Count,
            capped,
            note = made == 0 ? "No workers of these teams have a time clock policy with these pay periods." : null,
        });
    }
}
