using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Changes a shift on the published schedule now, without republishing, as the shift page's published-shift actions do:
/// new times, another worker, or taking it off, with a reason and whether the worker asked for it.
/// </summary>
internal sealed class ChangePublishedShiftTool : WorkforceAdminToolBase
{
    public const string ToolName = "changePublishedShift";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "scheduleId": { "type": "string", "description": "The schedule, from listShiftSchedules." },
        "shiftId": { "type": "string", "description": "The shift's ID in the schedule, from getShiftSchedule (a shift that is on the published schedule)." },
        "operation": { "type": "string", "enum": ["ChangeTimes", "Reassign", "Remove"], "description": "ChangeTimes moves the shift on its day; Reassign gives it to another worker; Remove takes it off the published schedule." },
        "start": { "type": "string", "description": "For ChangeTimes: the new start in the team's time (HH:mm)." },
        "end": { "type": "string", "description": "For ChangeTimes: the new end in the team's time (HH:mm); at or before the start means the next day." },
        "workerId": { "type": "string", "description": "For Reassign: who takes the shift, by ID." },
        "workerName": { "type": "string", "description": "For Reassign: who takes the shift, by name." },
        "reason": { "type": "string", "description": "Why, for the change log and the worker." },
        "workerAsked": { "type": "boolean", "description": "Whether the worker asked for the change; then no predictability pay is owed for it. False by default." }
      },
      "required": ["scheduleId", "shiftId", "operation"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Changes a published shift: new times, another worker, or removal, with a reason and whether the worker asked for it. The worker is notified and a late change can record predictability pay.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageSchedules;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (period, team, error) = await ShiftScheduleToolSupport.FindPeriodAsync(services, user, WorkforceAdminTools.Read(arguments, "scheduleId"), cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var shiftId = WorkforceAdminTools.Read(arguments, "shiftId");
        var draft = await ShiftScheduleToolSupport.FindDraftAsync(services, period, shiftId, cancellationToken);

        if (draft is null)
        {
            return $"The schedule has no shift with the ID '{shiftId}'. Use getShiftSchedule to find it.";
        }

        if (await ShiftScheduleToolSupport.FindPublishedAsync(services, draft, cancellationToken) is not { } published)
        {
            return "This shift isn't on the published schedule. Change it with saveDraftShift and publish the schedule.";
        }

        var operation = WorkforceAdminTools.Read(arguments, "operation");
        var shifts = services.GetRequiredService<IShiftService>();
        var initiator = WorkforceAdminTools.ReadBool(arguments, "workerAsked") == true ? ScheduleChangeInitiator.Worker : ScheduleChangeInitiator.Employer;
        var reason = WorkforceAdminTools.Read(arguments, "reason");
        var actor = user.Identity?.Name;
        var timeZoneId = await ShiftScheduleToolSupport.TimeZoneAsync(services, team);
        PlanningResult<CalendarEvent> result;

        if (string.Equals(operation, "ChangeTimes", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlanningInput.TryParseTime(WorkforceAdminTools.Read(arguments, "start"), out var from) || !PlanningInput.TryParseTime(WorkforceAdminTools.Read(arguments, "end"), out var to))
            {
                return "Give the new start and end (start, end) as HH:mm.";
            }

            var day = DateOnly.FromDateTime(CalendarTime.ToLocal(published.Event.StartUtc, timeZoneId));
            var startLocal = day.ToDateTime(from);
            var endLocal = to <= from ? day.AddDays(1).ToDateTime(to) : day.ToDateTime(to);

            result = await shifts.ChangeTimesAsync(published.Event.ItemId, CalendarTime.ToUtc(startLocal, timeZoneId), CalendarTime.ToUtc(endLocal, timeZoneId), initiator, reason, actor, cancellationToken);
        }
        else if (string.Equals(operation, "Reassign", StringComparison.OrdinalIgnoreCase))
        {
            var (worker, workerError) = await WorkforceAdminTools.FindWorkerAsync(services, user, arguments, cancellationToken);

            if (workerError is not null)
            {
                return workerError;
            }

            result = await shifts.ReassignAsync(published.Event.ItemId, worker.ItemId, initiator, reason, actor, cancellationToken);
        }
        else if (string.Equals(operation, "Remove", StringComparison.OrdinalIgnoreCase))
        {
            result = await shifts.RemoveAsync(published.Event.ItemId, initiator, reason, actor, cancellationToken);
        }
        else
        {
            return "The operation must be ChangeTimes, Reassign or Remove.";
        }

        if (!result.Succeeded)
        {
            return "The published shift was not changed: " + string.Join(" ", result.Errors);
        }

        var changed = result.Value ?? published.Event;
        var workerId = (changed.TryGet<ShiftData>(out var data) ? data.WorkerId : null) ?? published.Data.WorkerId;
        var names = await ShiftScheduleToolSupport.WorkerNamesAsync(services, [workerId], cancellationToken);

        return WorkforceAdminTools.Json(new
        {
            changed = true,
            operation,
            scheduleId = period.ItemId,
            shiftId = draft.ItemId,
            team = team.Name,
            timeZone = timeZoneId,
            removed = string.Equals(operation, "Remove", StringComparison.OrdinalIgnoreCase),
            worker = workerId is null ? null : names.GetValueOrDefault(workerId),
            start = CalendarTime.ToLocal(changed.StartUtc, timeZoneId).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture),
            end = CalendarTime.ToLocal(changed.EndUtc, timeZoneId).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture),
            initiatedBy = initiator.ToString(),
            note = "The worker was notified; a late change made by the employer can record predictability pay.",
        });
    }
}
