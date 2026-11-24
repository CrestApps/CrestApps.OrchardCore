using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Gets a schedule as the planner board shows it: its draft shifts by worker and day, the shifts nobody has, coverage
/// against the team's requirements and the labor rule problems; and, when asked, its change log.
/// </summary>
internal sealed class GetShiftScheduleTool : WorkforceAdminToolBase
{
    public const string ToolName = "getShiftSchedule";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "scheduleId": { "type": "string", "description": "The schedule's ID, from listShiftSchedules." },
        "from": { "type": "string", "description": "Optional. Only the shifts and coverage from this day (yyyy-MM-dd). Defaults to the schedule's first day." },
        "to": { "type": "string", "description": "Optional. Only the shifts and coverage up to this day (yyyy-MM-dd). Defaults to the schedule's last day." },
        "includeChanges": { "type": "boolean", "description": "Optional. Also return the latest changes made to the published schedule (who, what, when and why)." }
      },
      "required": ["scheduleId"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gets a schedule period: its draft shifts by worker and day, the shifts nobody has, where coverage is short or under target, the labor rule problems and, when asked, the log of changes made after publishing. Times are the team's wall-clock times.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageSchedules;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (period, team, error) = await ShiftScheduleToolSupport.FindPeriodAsync(services, user, WorkforceAdminTools.Read(arguments, "scheduleId"), cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var from = WorkforceAdminTools.ReadDate(arguments, "from") ?? period.StartDate;
        var to = WorkforceAdminTools.ReadDate(arguments, "to") ?? period.EndDate;

        if ((WorkforceAdminTools.Has(arguments, "from") && WorkforceAdminTools.ReadDate(arguments, "from") is null)
            || (WorkforceAdminTools.Has(arguments, "to") && WorkforceAdminTools.ReadDate(arguments, "to") is null))
        {
            return "Give the days as yyyy-MM-dd.";
        }

        from = from < period.StartDate ? period.StartDate : from;
        to = to > period.EndDate ? period.EndDate : to;

        if (to < from)
        {
            return $"The schedule runs from {WorkforceAdminTools.Day(period.StartDate)} to {WorkforceAdminTools.Day(period.EndDate)}; choose days inside it.";
        }

        var planning = services.GetRequiredService<ISchedulePlanningService>();
        var drafts = await planning.ListDraftShiftsAsync(period.PrimaryScenarioId, cancellationToken);
        var templates = (await services.GetRequiredService<IShiftTemplateManager>().GetAllAsync(cancellationToken)).ToDictionary(template => template.ItemId, StringComparer.Ordinal);
        var codes = (await services.GetRequiredService<IActivityCodeManager>().GetAllAsync(cancellationToken)).ToList();
        var inRange = drafts
            .Where(draft => draft.Date >= from && draft.Date <= to)
            .OrderBy(draft => draft.Date)
            .ThenBy(draft => draft.Start)
            .ToList();
        var names = await ShiftScheduleToolSupport.WorkerNamesAsync(services, inRange.Select(draft => draft.WorkerId), cancellationToken);
        IReadOnlyList<LaborRuleViolation> violations = drafts.Count == 0 ? [] : await planning.CheckPublishAsync(period.ItemId, cancellationToken);
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var timeZoneId = await ShiftScheduleToolSupport.TimeZoneAsync(services, team);

        object changes = null;

        if (WorkforceAdminTools.ReadBool(arguments, "includeChanges") == true)
        {
            var all = (await planning.ListChangesAsync(period.ItemId, cancellationToken)).Reverse().ToList();
            var changeNames = await ShiftScheduleToolSupport.WorkerNamesAsync(services, all.Take(ShiftScheduleToolSupport.MaxProblems).SelectMany(change => new[] { change.WorkerId, change.PreviousWorkerId }), cancellationToken);
            var localClock = services.GetRequiredService<ILocalClock>();
            var items = new List<object>();

            foreach (var change in all.Take(ShiftScheduleToolSupport.MaxProblems))
            {
                items.Add(new
                {
                    kind = change.Kind.ToString(),
                    worker = change.WorkerId is null ? null : changeNames.GetValueOrDefault(change.WorkerId),
                    previousWorker = change.PreviousWorkerId is null ? null : changeNames.GetValueOrDefault(change.PreviousWorkerId),
                    before = Range(change.BeforeStartUtc, change.BeforeEndUtc, timeZoneId),
                    after = Range(change.AfterStartUtc, change.AfterEndUtc, timeZoneId),
                    initiatedBy = change.InitiatedBy.ToString(),
                    reason = change.Reason,
                    changedBy = change.ChangedBy,
                    changedAt = await WorkforceAdminTools.LocalAsync(localClock, change.ChangedUtc),
                });
            }

            changes = new
            {
                total = all.Count,
                items,
                truncated = all.Count > items.Count,
            };
        }

        return WorkforceAdminTools.Json(new
        {
            id = period.ItemId,
            name = period.Name,
            teamId = team.ItemId,
            team = team.Name,
            timeZone = timeZoneId,
            startDate = WorkforceAdminTools.Day(period.StartDate),
            endDate = WorkforceAdminTools.Day(period.EndDate),
            status = period.Status.ToString(),
            publishedVersion = period.PublishedVersion,
            from = WorkforceAdminTools.Day(from),
            to = WorkforceAdminTools.Day(to),
            shiftCount = inRange.Count,
            openShiftCount = inRange.Count(draft => draft.WorkerId is null),
            shifts = inRange.Take(ShiftScheduleToolSupport.MaxShifts).Select(draft => ShiftScheduleToolSupport.Describe(draft, names, templates, codes)).ToList(),
            shiftsTruncated = inRange.Count > ShiftScheduleToolSupport.MaxShifts,
            coverage = await ShiftScheduleToolSupport.CoverageAsync(services, period, team, from, to, cancellationToken),
            laborRuleProblems = ShiftScheduleToolSupport.Problems(violations),
            laborRuleProblemCount = violations.Count,
            canPublish = await authorization.AuthorizeAsync(user, WorkforcePermissions.PublishSchedules),
            canOverrideLaborRules = await authorization.AuthorizeAsync(user, WorkforcePermissions.OverrideLaborRules),
            changes,
            link = ShiftScheduleToolSupport.PlanPath(services, period.ItemId),
        });
    }

    private static object Range(DateTime? startUtc, DateTime? endUtc, string timeZoneId)
    {
        if (startUtc is null)
        {
            return null;
        }

        return new
        {
            start = CalendarTime.ToLocal(DateTime.SpecifyKind(startUtc.Value, DateTimeKind.Utc), timeZoneId).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture),
            end = endUtc is null ? null : CalendarTime.ToLocal(DateTime.SpecifyKind(endUtc.Value, DateTimeKind.Utc), timeZoneId).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture),
        };
    }
}
