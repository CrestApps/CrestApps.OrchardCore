using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using static CrestApps.OrchardCore.Workforce.ContactCenter.AI.ContactCenterWorkforceTools;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Gets an agent's adherence over a day, as the worker's adherence page shows it: the saved day, or the day computed now
/// when none is saved.
/// </summary>
internal sealed class GetAdherenceDayTool : ContactCenterWorkforceToolBase
{
    public const string ToolName = "getAdherenceDay";

    private const int MaxStretches = 100;

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "workerId": { "type": "string", "description": "The worker's ID. Give this or workerName." },
        "workerName": { "type": "string", "description": "The worker's name, when the ID isn't known." },
        "date": { "type": "string", "description": "Optional. The day, yyyy-MM-dd, in the worker's time zone. Today by default." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gets an agent's adherence for a day: adherence and conformance percent, minutes scheduled, in adherence and excepted, each stretch out of adherence with the scheduled activity and the actual state, and the exceptions recorded. Uses the saved day, or computes it now when it isn't saved yet.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ViewAdherence;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (worker, error) = await FindWorkerAsync(services, user, arguments, cancellationToken);

        if (worker is null)
        {
            return error;
        }

        if (Has(arguments, "date") && ReadDate(arguments, "date") is null)
        {
            return "The date must be a day written yyyy-MM-dd.";
        }

        var adherence = services.GetRequiredService<AdherenceService>();
        var zone = await adherence.TimeZoneOfAsync(worker, cancellationToken);
        var date = ReadDate(arguments, "date") ?? Today(services, zone);
        var saved = await adherence.FindDayAsync(worker.ItemId, date, cancellationToken);
        var day = saved ?? await adherence.ComputeDayAsync(worker, date, cancellationToken);

        if (day is null)
        {
            return $"{worker.DisplayName} is not a contact center agent, so there is no adherence to show.";
        }

        var codes = (await services.GetRequiredService<IActivityCodeManager>().GetAllAsync(cancellationToken))
            .ToDictionary(code => code.ItemId, code => code.Name, StringComparer.Ordinal);
        var fromUtc = CalendarTime.ToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
        var toUtc = CalendarTime.ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var exceptions = await adherence.ListExceptionsAsync(worker.ItemId, fromUtc, toUtc, cancellationToken);
        var outStretches = day.Segments.Where(segment => !segment.InAdherence && !segment.Excepted).OrderBy(segment => segment.StartUtc).ToList();
        var counted = day.ScheduledSeconds - day.ExceptionSeconds;

        return ToJson(new
        {
            worker = new { id = worker.ItemId, name = worker.DisplayName },
            date = Day(date),
            timeZone = zone,
            saved = saved is not null,
            computedAt = await LocalAsync(services, day.ComputedUtc),
            adherencePercent = Percent(day.Adherence),
            conformancePercent = Percent(day.Conformance),
            scheduledMinutes = Minutes(day.ScheduledSeconds),
            inAdherenceMinutes = Minutes(day.InAdherenceSeconds),
            exceptionMinutes = Minutes(day.ExceptionSeconds),
            outOfAdherenceMinutes = Minutes(Math.Max(0, counted - day.InAdherenceSeconds)),
            scheduledWorkMinutes = Minutes(day.ScheduledWorkSeconds),
            actualWorkMinutes = Minutes(day.ActualWorkSeconds),
            outOfAdherence = outStretches.Take(MaxStretches).Select(segment => new
            {
                start = InZone(segment.StartUtc, zone),
                end = InZone(segment.EndUtc, zone),
                minutes = Minutes(segment.Seconds),
                scheduled = segment.Kind.ToString(),
                activity = segment.ActivityCodeId is not null && codes.TryGetValue(segment.ActivityCodeId, out var code) ? code : null,
                state = segment.Status.ToString(),
                reason = segment.Reason,
            }),
            outOfAdherenceStretches = outStretches.Count,
            truncated = outStretches.Count > MaxStretches,
            exceptions = exceptions.OrderBy(item => item.StartUtc).Select(item => new
            {
                id = item.ItemId,
                start = InZone(item.StartUtc, zone),
                end = InZone(item.EndUtc, zone),
                reason = item.Reason.ToString(),
                note = item.Note,
                recordedBy = item.RecordedBy,
            }),
            note = saved is null
                ? "This day isn't saved yet, so it was computed now; the history saves each day after it ends."
                : "This is the saved day. Exceptions recorded after it was saved count once the day is saved again (addAdherenceException with recomputeDay).",
        });
    }
}
