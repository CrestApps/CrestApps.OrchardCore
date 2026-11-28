using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Lists the predictability pay entries of the teams the user manages, as the Predictability Pay page does.
/// </summary>
internal sealed class ListPredictabilityPayTool : WorkforceAdminToolBase
{
    public const string ToolName = "listPredictabilityPay";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "teamName": { "type": "string", "description": "Optional. A team the user manages, by name or ID; its sub-teams are included. Leave out for every team the user manages." },
        "workerId": { "type": "string", "description": "Optional. Only this worker's entries, by ID." },
        "workerName": { "type": "string", "description": "Optional. Only this worker's entries, by name." },
        "status": { "type": "string", "enum": ["Pending", "Approved", "Waived", "Exported", "All"], "description": "Which entries: Pending (the default) waits for a decision; All shows every status." },
        "page": { "type": "integer", "minimum": 1, "description": "The page, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Entries per page, 20 by default, at most 50." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists the predictability pay entries raised by late schedule changes, filtered by team, worker and status, with the change that caused each, the notice given and the hours owed.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApprovePredictabilityPay;

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

        PredictabilityPayStatus? status = PredictabilityPayStatus.Pending;
        var statusText = WorkforceAdminTools.Read(arguments, "status");

        if (string.Equals(statusText, "All", StringComparison.OrdinalIgnoreCase))
        {
            status = null;
        }
        else if (statusText is not null)
        {
            if (!WorkforceAdminTools.TryReadEnum<PredictabilityPayStatus>(arguments, "status", out var parsed))
            {
                return "The status must be Pending, Approved, Waived, Exported or All.";
            }

            status = parsed;
        }

        var (page, pageSize) = WorkforceAdminTools.ReadPaging(arguments);
        var (count, entries) = await services.GetRequiredService<IPredictabilityPayService>().PageAsync(new PredictabilityPayQuery
        {
            TeamIds = teamIds,
            WorkerId = workerId,
            Status = status,
        }, (page - 1) * pageSize, pageSize, cancellationToken);

        var teams = (await services.GetRequiredService<ITeamManager>().ListTreeAsync(cancellationToken)).ToDictionary(entry => entry.Team.ItemId, entry => entry.Team, StringComparer.Ordinal);
        var names = await ShiftScheduleToolSupport.WorkerNamesAsync(services, entries.Select(entry => entry.WorkerId), cancellationToken);
        var localClock = services.GetRequiredService<ILocalClock>();
        var zones = new Dictionary<string, string>(StringComparer.Ordinal);
        var rows = new List<object>();

        foreach (var entry in entries)
        {
            var team = teams.GetValueOrDefault(entry.TeamId ?? string.Empty);
            var teamKey = entry.TeamId ?? string.Empty;

            if (!zones.TryGetValue(teamKey, out var timeZoneId))
            {
                zones[teamKey] = timeZoneId = await TimeZoneAsync(services, team);
            }

            rows.Add(new
            {
                id = entry.ItemId,
                workerId = entry.WorkerId,
                worker = entry.WorkerId is null ? null : names.GetValueOrDefault(entry.WorkerId),
                team = team?.Name,
                change = entry.ChangeKind.ToString(),
                scheduleChangeId = entry.ScheduleChangeId,
                scheduleId = entry.PeriodId,
                shiftStart = WorkforceAdminTools.InZone(entry.ShiftStartUtc, timeZoneId),
                timeZone = timeZoneId,
                noticeHours = entry.NoticeHours,
                hoursOwed = entry.Hours,
                status = entry.Status.ToString(),
                note = entry.Note,
                decidedBy = entry.DecidedBy,
                decidedAt = await WorkforceAdminTools.LocalAsync(localClock, entry.DecidedUtc),
                raisedAt = await WorkforceAdminTools.LocalAsync(localClock, entry.CreatedUtc),
            });
        }

        return WorkforceAdminTools.Json(WorkforceAdminTools.Paged(rows, count, page, pageSize));
    }

    private static Task<string> TimeZoneAsync(IServiceProvider services, Team team)
        => ShiftScheduleToolSupport.TimeZoneAsync(services, team);
}
