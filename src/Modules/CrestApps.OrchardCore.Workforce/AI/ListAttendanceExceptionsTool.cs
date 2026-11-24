using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Lists attendance exceptions of the teams the user manages, as the Attendance page does.
/// </summary>
internal sealed class ListAttendanceExceptionsTool : WorkforceAdminToolBase
{
    public const string ToolName = "listAttendanceExceptions";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "teamName": { "type": "string", "description": "Optional. A team the user manages, by name or ID; its sub-teams are included. Leave out for every team the user manages." },
        "workerId": { "type": "string", "description": "Optional. Only this worker's exceptions, by ID." },
        "workerName": { "type": "string", "description": "Optional. Only this worker's exceptions, by name." },
        "kind": { "type": "string", "enum": ["Late", "EarlyOut", "MissedPunch", "NoShow", "UnscheduledAbsence", "LongMeal", "OutsideGeofence"], "description": "Optional. Only this kind: late start, early finish, missed punch, no-show, unscheduled absence, long meal or away from the site." },
        "status": { "type": "string", "enum": ["Open", "Confirmed", "Excused", "All"], "description": "Which exceptions: Open (the default) waits for a decision; All shows every status." },
        "from": { "type": "string", "description": "Optional. Only exceptions on or after this day (yyyy-MM-dd)." },
        "to": { "type": "string", "description": "Optional. Only exceptions on or before this day (yyyy-MM-dd)." },
        "page": { "type": "integer", "minimum": 1, "description": "The page, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Exceptions per page, 20 by default, at most 50." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists attendance exceptions (late, left early, absent, missed punch) filtered by team, worker, kind, status and dates, with the worker's current attendance points.";

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

        AttendanceExceptionStatus? status = AttendanceExceptionStatus.Open;
        var statusText = WorkforceAdminTools.Read(arguments, "status");

        if (string.Equals(statusText, "All", StringComparison.OrdinalIgnoreCase))
        {
            status = null;
        }
        else if (statusText is not null)
        {
            if (!WorkforceAdminTools.TryReadEnum<AttendanceExceptionStatus>(arguments, "status", out var parsed))
            {
                return "The status must be Open, Confirmed, Excused or All.";
            }

            status = parsed;
        }

        AttendanceExceptionKind? kind = null;

        if (WorkforceAdminTools.Has(arguments, "kind"))
        {
            if (!WorkforceAdminTools.TryReadEnum<AttendanceExceptionKind>(arguments, "kind", out var parsedKind))
            {
                return "The kind must be one of: " + string.Join(", ", Enum.GetNames<AttendanceExceptionKind>()) + ".";
            }

            kind = parsedKind;
        }

        var (page, pageSize) = WorkforceAdminTools.ReadPaging(arguments);
        var attendance = services.GetRequiredService<IAttendanceService>();
        var (count, items) = await attendance.PageAsync(new AttendanceQuery
        {
            TeamIds = teamIds,
            WorkerId = workerId,
            Status = status,
            Kind = kind,
            From = WorkforceAdminTools.ReadDate(arguments, "from"),
            To = WorkforceAdminTools.ReadDate(arguments, "to"),
        }, (page - 1) * pageSize, pageSize, cancellationToken);

        var workers = services.GetRequiredService<IStaffMemberManager>();
        var teams = (await services.GetRequiredService<ITeamManager>().ListTreeAsync(cancellationToken)).ToDictionary(entry => entry.Team.ItemId, entry => entry.Team.Name, StringComparer.Ordinal);
        var today = await WorkforceAdminTools.TodayAsync(services);
        var people = new Dictionary<string, (StaffMember Worker, decimal Points)>(StringComparer.Ordinal);
        var rows = new List<object>();

        foreach (var exception in items)
        {
            if (!people.TryGetValue(exception.WorkerId ?? string.Empty, out var person))
            {
                var worker = string.IsNullOrEmpty(exception.WorkerId) ? null : await workers.FindByIdAsync(exception.WorkerId, cancellationToken);

                person = (worker, worker is null ? 0 : await attendance.GetPointsAsync(worker, today, cancellationToken));
                people[exception.WorkerId ?? string.Empty] = person;
            }

            rows.Add(new
            {
                id = exception.ItemId,
                worker = person.Worker?.DisplayName,
                workerId = exception.WorkerId,
                team = teams.GetValueOrDefault(exception.TeamId ?? string.Empty),
                date = WorkforceAdminTools.Day(exception.Date),
                kind = exception.Kind.ToString(),
                minutes = exception.Minutes,
                status = exception.Status.ToString(),
                points = exception.Points,
                workerPoints = person.Points,
                note = exception.Note,
                decidedBy = exception.DecidedBy,
            });
        }

        return WorkforceAdminTools.Json(WorkforceAdminTools.Paged(rows, count, page, pageSize));
    }
}
