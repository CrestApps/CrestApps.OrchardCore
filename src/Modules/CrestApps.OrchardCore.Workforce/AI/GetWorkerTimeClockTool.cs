using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Gets a worker's time clock setup as the worker's time clock page shows it, without the PIN or any kiosk address.
/// </summary>
internal sealed class GetWorkerTimeClockTool : WorkforceAdminToolBase
{
    public const string ToolName = "getWorkerTimeClock";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "workerId": { "type": "string", "description": "The worker's ID. Give this or workerName." },
        "workerName": { "type": "string", "description": "The worker's name. Give this or workerId." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gets a worker's time clock setup: the policy that applies, whether they have a kiosk PIN (never the PIN itself), whether it is locked after wrong tries, whether they are clocked in, and their attendance points.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageWorkers;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (worker, error) = await WorkforceAdminTools.FindWorkerAsync(services, user, arguments, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var today = await WorkforceAdminTools.TodayAsync(services);
        var timeClock = services.GetRequiredService<ITimeClockService>();
        var localClock = services.GetRequiredService<ILocalClock>();
        var badge = worker.TryGet<StaffBadge>(out var stored) ? stored : new StaffBadge();
        var policy = await timeClock.GetPolicyAsync(worker, today, cancellationToken);
        var status = await timeClock.GetStatusAsync(worker, cancellationToken);
        var points = await services.GetRequiredService<IAttendanceService>().GetPointsAsync(worker, today, cancellationToken);

        return WorkforceAdminTools.Json(new
        {
            worker = new { id = worker.ItemId, name = worker.DisplayName },
            policy = policy is null ? null : new { id = policy.ItemId, name = policy.Name },
            hasPin = !string.IsNullOrEmpty(badge.PinHash),
            pinLockedUntil = await WorkforceAdminTools.LocalAsync(localClock, badge.LockedUntilUtc),
            clock = new
            {
                state = status.State.ToString(),
                since = await WorkforceAdminTools.LocalAsync(localClock, status.SinceUtc),
                lastPunch = status.LastPunch is null ? null : new { kind = status.LastPunch.Kind.ToString(), time = await WorkforceAdminTools.LocalAsync(localClock, status.LastPunch.Utc) },
                nextPunches = status.NextPunches.Select(kind => kind.ToString()),
            },
            attendancePoints = points,
            adminPath = WorkforceAdminTools.GetAdminPath(services, "WorkerTimeClock", "Index", new { workerId = worker.ItemId }),
        });
    }
}
