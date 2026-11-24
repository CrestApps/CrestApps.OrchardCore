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
/// Lists the time-off requests of the teams the user manages, as the Time-Off Requests queue does.
/// </summary>
internal sealed class ListTimeOffRequestsTool : WorkforceAdminToolBase
{
    public const string ToolName = "listTimeOffRequests";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "teamName": { "type": "string", "description": "Optional. A team the user manages, by name or ID; its sub-teams are included. Leave out for every team the user manages." },
        "workerId": { "type": "string", "description": "Optional. Only this worker's requests, by ID." },
        "workerName": { "type": "string", "description": "Optional. Only this worker's requests, by name." },
        "status": { "type": "string", "enum": ["Waiting", "Pending", "Waitlisted", "Approved", "Declined", "Canceled", "Withdrawn"], "description": "Which requests. Waiting (the default) is every request that waits for a decision: pending and waitlisted." },
        "from": { "type": "string", "description": "Optional. Only requests that end on or after this day (yyyy-MM-dd)." },
        "to": { "type": "string", "description": "Optional. Only requests that start on or before this day (yyyy-MM-dd)." },
        "page": { "type": "integer", "minimum": 1, "description": "The page, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Requests per page, 20 by default, at most 50." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists time-off requests of the teams the user manages, filtered by team, worker, status and dates, with the days asked for and the balance left. Use it for \"what is waiting for me\".";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApproveTimeOff;

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

        TimeOffRequestStatus? status = null;
        var statusText = WorkforceAdminTools.Read(arguments, "status");

        if (statusText is not null && !statusText.Equals("Waiting", StringComparison.OrdinalIgnoreCase))
        {
            if (!WorkforceAdminTools.TryReadEnum<TimeOffRequestStatus>(arguments, "status", out var parsed))
            {
                return "The status must be Waiting, Pending, Waitlisted, Approved, Declined, Canceled or Withdrawn.";
            }

            status = parsed;
        }

        var (page, pageSize) = WorkforceAdminTools.ReadPaging(arguments);
        var requests = services.GetRequiredService<ITimeOffRequestService>();
        var (count, items) = await requests.PageAsync(new TimeOffRequestQuery
        {
            TeamIds = teamIds,
            WorkerId = workerId,
            Status = status,
            From = WorkforceAdminTools.ReadDate(arguments, "from"),
            To = WorkforceAdminTools.ReadDate(arguments, "to"),
        }, (page - 1) * pageSize, pageSize, cancellationToken);

        var types = (await services.GetRequiredService<ITimeOffTypeManager>().GetAllAsync(cancellationToken)).ToDictionary(type => type.ItemId, StringComparer.Ordinal);
        var workers = services.GetRequiredService<IStaffMemberManager>();
        var teams = services.GetRequiredService<ITeamManager>();
        var balances = services.GetRequiredService<ITimeOffBalanceService>();
        var localClock = services.GetRequiredService<ILocalClock>();
        var rows = new List<object>();

        foreach (var request in items)
        {
            var type = types.GetValueOrDefault(request.TypeId ?? string.Empty);
            decimal? available = null;

            if (request.IsOpen && !string.IsNullOrEmpty(request.PolicyId))
            {
                available = (await balances.GetBalanceAsync(request.WorkerId, request.PolicyId, request.StartDate, cancellationToken))?.Available;
            }

            rows.Add(new
            {
                id = request.ItemId,
                worker = (await workers.FindByIdAsync(request.WorkerId, cancellationToken))?.DisplayName,
                workerId = request.WorkerId,
                team = string.IsNullOrEmpty(request.TeamId) ? null : (await teams.FindByIdAsync(request.TeamId, cancellationToken))?.Name,
                type = type?.Name,
                unit = type?.Unit.ToString(),
                startDate = WorkforceAdminTools.Day(request.StartDate),
                endDate = WorkforceAdminTools.Day(request.EndDate),
                startTime = WorkforceAdminTools.Time(request.StartTime),
                endTime = WorkforceAdminTools.Time(request.EndTime),
                amount = request.Amount,
                status = request.Status.ToString(),
                reason = request.Reason,
                decisionNote = request.DecisionNote,
                submittedBy = request.SubmittedBy,
                submitted = await WorkforceAdminTools.LocalAsync(localClock, request.SubmittedUtc),
                availableAfterWaitingRequests = available,
                adminPath = WorkforceAdminTools.GetAdminPath(services, "TimeOff", "Details", new { id = request.ItemId }),
            });
        }

        return WorkforceAdminTools.Json(WorkforceAdminTools.Paged(rows, count, page, pageSize));
    }
}
