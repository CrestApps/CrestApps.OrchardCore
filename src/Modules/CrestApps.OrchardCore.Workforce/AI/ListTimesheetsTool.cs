using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Lists the timesheets of the teams the user manages, as the Timesheets page does.
/// </summary>
internal sealed class ListTimesheetsTool : WorkforceAdminToolBase
{
    public const string ToolName = "listTimesheets";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "teamName": { "type": "string", "description": "Optional. A team the user manages, by name or ID; its sub-teams are included. Leave out for every team the user manages." },
        "workerId": { "type": "string", "description": "Optional. Only this worker's timesheets, by ID." },
        "workerName": { "type": "string", "description": "Optional. Only this worker's timesheets, by name." },
        "status": { "type": "string", "enum": ["Open", "Submitted", "Rejected", "Approved", "Reopened", "Locked"], "description": "Optional. Only timesheets in this status. Submitted ones wait for approval; Rejected ones were sent back to the worker." },
        "payPeriodCalendarId": { "type": "string", "description": "Optional. Only timesheets of these pay periods, by the pay period calendar's ID shown on each timesheet." },
        "periodStart": { "type": "string", "description": "Optional. Only the pay period that starts on this day (yyyy-MM-dd), as shown on each timesheet." },
        "page": { "type": "integer", "minimum": 1, "description": "The page, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Timesheets per page, 20 by default, at most 50." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists timesheets filtered by pay period, team, worker and status, with each one's total and worked hours and open exceptions. Use getTimesheet for the hours by pay code, overtime included.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApproveTimesheets;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (teamIds, teamError) = await WorkforceAdminTools.TeamFilterAsync(services, user, arguments, cancellationToken);

        if (teamError is not null)
        {
            return teamError;
        }

        string workerId = null;

        if (WorkforceAdminTools.Has(arguments, "workerId") || WorkforceAdminTools.Has(arguments, "workerName"))
        {
            var (worker, workerError) = await WorkforceAdminTools.FindWorkerAsync(services, user, arguments, cancellationToken);

            if (workerError is not null)
            {
                return workerError;
            }

            workerId = worker.ItemId;
        }

        IReadOnlyCollection<TimesheetStatus> statuses = [];

        if (WorkforceAdminTools.Has(arguments, "status"))
        {
            if (!WorkforceAdminTools.TryReadEnum<TimesheetStatus>(arguments, "status", out var status))
            {
                return "The status must be one of: " + TimeClockAdminTools.StatusList + ".";
            }

            statuses = [status];
        }

        var (page, pageSize) = WorkforceAdminTools.ReadPaging(arguments);
        var (count, items) = await services.GetRequiredService<ITimesheetService>().PageAsync(new TimesheetQuery
        {
            TeamIds = teamIds,
            WorkerId = workerId,
            Statuses = statuses,
            PayPeriodCalendarId = WorkforceAdminTools.Read(arguments, "payPeriodCalendarId"),
            PeriodStart = WorkforceAdminTools.ReadDate(arguments, "periodStart"),
        }, (page - 1) * pageSize, pageSize, cancellationToken);

        var workers = services.GetRequiredService<IStaffMemberManager>();
        var teams = (await services.GetRequiredService<ITeamManager>().ListTreeAsync(cancellationToken)).ToDictionary(entry => entry.Team.ItemId, entry => entry.Team.Name, StringComparer.Ordinal);
        var localClock = services.GetRequiredService<ILocalClock>();
        var rows = new List<object>();

        foreach (var timesheet in items)
        {
            rows.Add(new
            {
                id = timesheet.ItemId,
                worker = (await workers.FindByIdAsync(timesheet.WorkerId, cancellationToken))?.DisplayName,
                workerId = timesheet.WorkerId,
                team = teams.GetValueOrDefault(timesheet.TeamId ?? string.Empty),
                payPeriodCalendarId = timesheet.PayPeriodCalendarId,
                periodStart = WorkforceAdminTools.Day(timesheet.PeriodStart),
                periodEnd = WorkforceAdminTools.Day(timesheet.PeriodEnd),
                status = timesheet.Status.ToString(),
                totalHours = timesheet.TotalHours,
                workedHours = Math.Round(timesheet.WorkedMinutes / 60m, 2),
                openExceptions = timesheet.OpenExceptions,
                submitted = await WorkforceAdminTools.LocalAsync(localClock, timesheet.SubmittedUtc),
                decidedBy = timesheet.DecidedBy,
                rejectionReason = timesheet.RejectionReason,
                adminPath = WorkforceAdminTools.GetAdminPath(services, "Timesheets", "Details", new { id = timesheet.ItemId }),
            });
        }

        return WorkforceAdminTools.Json(WorkforceAdminTools.Paged(rows, count, page, pageSize));
    }
}
