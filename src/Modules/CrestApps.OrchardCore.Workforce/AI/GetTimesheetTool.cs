using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Portal;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Gets one timesheet as its admin page shows it, built by the same <see cref="TimesheetViews"/>.
/// </summary>
internal sealed class GetTimesheetTool : WorkforceAdminToolBase
{
    public const string ToolName = "getTimesheet";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "timesheetId": { "type": "string", "description": "The timesheet's ID, from listTimesheets. Or give a worker and a date." },
        "workerId": { "type": "string", "description": "With date: the worker's ID." },
        "workerName": { "type": "string", "description": "With date: the worker's name." },
        "date": { "type": "string", "description": "With a worker: a day in the pay period (yyyy-MM-dd); today by default." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gets one timesheet (by id, or a worker and a date): each day's punches and hours, the totals by pay code, manual lines, attendance exceptions and pending punch corrections. Times are in the worker's time zone.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApproveTimesheets;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var timesheets = services.GetRequiredService<ITimesheetService>();
        var timesheetId = WorkforceAdminTools.Read(arguments, "timesheetId");
        Timesheet timesheet;

        if (timesheetId is not null)
        {
            var (found, error) = await TimeClockAdminTools.FindTimesheetAsync(services, user, timesheetId, cancellationToken);

            if (error is not null)
            {
                return error;
            }

            timesheet = found;
        }
        else
        {
            var (worker, workerError) = await WorkforceAdminTools.FindWorkerAsync(services, user, arguments, cancellationToken);

            if (workerError is not null)
            {
                return workerError == "Give the worker's ID (workerId) or name (workerName)."
                    ? "Give the timesheet's ID, or a worker and a date."
                    : workerError;
            }

            var date = WorkforceAdminTools.ReadDate(arguments, "date") ?? await WorkforceAdminTools.TodayAsync(services);
            var (_, items) = await timesheets.PageAsync(new TimesheetQuery { WorkerId = worker.ItemId }, 0, 100, cancellationToken);

            // Read only: the admin page's "worker's timesheet" link creates a missing one, which prepareTimesheets does here.
            timesheet = items.FirstOrDefault(item => item.PeriodStart <= date && date <= item.PeriodEnd);

            if (timesheet is null)
            {
                return $"{worker.DisplayName} has no timesheet for the pay period of {WorkforceAdminTools.Day(date)}. Use prepareTimesheets for their team to create it.";
            }

            var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);

            if (!scope.Covers(timesheet.TeamId))
            {
                return "The user doesn't manage this timesheet's team.";
            }
        }

        var staff = await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(timesheet.WorkerId, cancellationToken);

        if (staff is null)
        {
            return "The timesheet's worker no longer exists.";
        }

        var model = await services.GetRequiredService<TimesheetViews>().BuildAsync(timesheet, staff, cancellationToken);
        var zone = model.TimeZoneId;
        var payCodes = model.PayCodes;
        var activities = model.ActivityNames;
        var localClock = services.GetRequiredService<ILocalClock>();
        var current = model.Timesheet;

        return WorkforceAdminTools.Json(new
        {
            id = current.ItemId,
            worker = new { id = staff.ItemId, name = staff.DisplayName },
            team = model.TeamName,
            payPeriodCalendarId = current.PayPeriodCalendarId,
            periodStart = WorkforceAdminTools.Day(current.PeriodStart),
            periodEnd = WorkforceAdminTools.Day(current.PeriodEnd),
            status = current.Status.ToString(),
            editable = current.IsEditable,
            totalHours = current.TotalHours,
            workedHours = Math.Round(current.WorkedMinutes / 60m, 2),
            openExceptions = current.OpenExceptions,
            submitted = await WorkforceAdminTools.LocalAsync(localClock, current.SubmittedUtc),
            attestation = current.Attestation,
            decidedBy = current.DecidedBy,
            decided = await WorkforceAdminTools.LocalAsync(localClock, current.DecidedUtc),
            rejectionReason = current.RejectionReason,
            locked = await WorkforceAdminTools.LocalAsync(localClock, current.LockedUtc),
            timeZoneId = zone,
            onTheClockSince = WorkforceAdminTools.InZone(model.OnTheClockSinceUtc, zone),
            totals = model.Totals.Select(total => new
            {
                payCodeId = total.Code.ItemId,
                code = total.Code.Code,
                name = total.Code.Name,
                kind = total.Code.Kind.ToString(),
                hours = total.Hours,
            }),
            days = model.Days.Select(day => new
            {
                date = WorkforceAdminTools.Day(day.Date),
                punches = day.Punches.Select(punch => new
                {
                    id = punch.ItemId,
                    kind = punch.Kind.ToString(),
                    time = WorkforceAdminTools.InZone(punch.Utc, zone),
                    source = punch.Source,
                    activity = string.IsNullOrEmpty(punch.ActivityCodeId) ? null : activities?.GetValueOrDefault(punch.ActivityCodeId),
                    reason = punch.Reason,
                    by = punch.Actor,
                }),
                lines = day.Lines.Select(line => new
                {
                    id = line.Id,
                    payCodeId = line.PayCodeId,
                    payCode = string.IsNullOrEmpty(line.PayCodeId) ? null : payCodes?.GetValueOrDefault(line.PayCodeId)?.Code,
                    hours = line.Hours,
                    source = line.Source.ToString(),
                    activity = string.IsNullOrEmpty(line.ActivityCodeId) ? null : activities?.GetValueOrDefault(line.ActivityCodeId),
                    note = line.Note,
                }),
                exceptions = day.Exceptions.Select(exception => new
                {
                    id = exception.ItemId,
                    kind = exception.Kind.ToString(),
                    minutes = exception.Minutes,
                    status = exception.Status.ToString(),
                    points = exception.Points,
                    note = exception.Note,
                }),
            }).Where(day => day.punches.Any() || day.lines.Any() || day.exceptions.Any()),
            pendingCorrections = model.Corrections.Select(correction => new
            {
                id = correction.ItemId,
                kind = correction.Kind.ToString(),
                time = WorkforceAdminTools.InZone(correction.Utc, zone),
                reason = correction.Reason,
            }),
            payCodes = payCodes.Values.Where(code => code.Enabled).Select(code => new
            {
                id = code.ItemId,
                code = code.Code,
                name = code.Name,
                kind = code.Kind.ToString(),
            }),
            adminPath = WorkforceAdminTools.GetAdminPath(services, "Timesheets", "Details", new { id = current.ItemId }),
        });
    }
}
