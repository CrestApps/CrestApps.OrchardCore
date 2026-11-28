using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using YesSql;
using static CrestApps.OrchardCore.Workforce.ContactCenter.AI.ContactCenterWorkforceTools;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Records an adherence exception for an agent of a team the user manages, as the Add exception page does, and can
/// compute and save the worker's day again, as the worker's adherence page's Save button does.
/// </summary>
internal sealed class AddAdherenceExceptionTool : ContactCenterWorkforceToolBase
{
    public const string ToolName = "addAdherenceException";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "workerId": { "type": "string", "description": "The worker's ID. Give this or workerName." },
        "workerName": { "type": "string", "description": "The worker's name, when the ID isn't known." },
        "date": { "type": "string", "description": "The day the exception starts, yyyy-MM-dd, in the worker's time zone." },
        "start": { "type": "string", "description": "The start time, HH:mm, in the worker's time zone." },
        "end": { "type": "string", "description": "The end time, HH:mm. An end at or before the start is on the next day." },
        "reason": { "type": "string", "enum": ["SystemOutage", "Coaching", "ApprovedScheduleChange", "Other"], "description": "Why the time doesn't count against the agent." },
        "note": { "type": "string", "description": "Optional. A note, such as who held the coaching session." },
        "recomputeDay": { "type": "boolean", "description": "Optional. Also compute the worker's day again and save it, so the exception counts at once; otherwise it counts once the day is saved (the history saves each day after it ends)." }
      },
      "required": ["date", "start", "end", "reason"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Records an adherence exception for an agent for a time range, with a reason and note, so that time does not count against them. With recomputeDay it also computes and saves the agent's day again.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageAdherence;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (worker, error) = await FindWorkerAsync(services, user, arguments, cancellationToken);

        if (worker is null)
        {
            return error;
        }

        var date = ReadDate(arguments, "date");
        var start = ReadTime(arguments, "start");
        var end = ReadTime(arguments, "end");

        if (date is null || start is null || end is null)
        {
            return "Give the day (yyyy-MM-dd) and the start and end times (HH:mm) in the worker's time zone.";
        }

        if (!TryParseEnum<AdherenceExceptionReason>(Read(arguments, "reason"), out var reason))
        {
            return "The reason must be SystemOutage, Coaching, ApprovedScheduleChange or Other.";
        }

        var adherence = services.GetRequiredService<AdherenceService>();
        var zone = await adherence.TimeZoneOfAsync(worker, cancellationToken);
        var endLocal = end.Value <= start.Value ? date.Value.AddDays(1).ToDateTime(end.Value) : date.Value.ToDateTime(end.Value);
        var exception = new AdherenceException
        {
            WorkerId = worker.ItemId,
            StartUtc = CalendarTime.ToUtc(date.Value.ToDateTime(start.Value), zone),
            EndUtc = CalendarTime.ToUtc(endLocal, zone),
            Reason = reason,
            Note = Read(arguments, "note"),
            RecordedBy = user.Identity?.Name,
        };
        var errors = await adherence.AddExceptionAsync(exception, cancellationToken);

        if (errors.Count > 0)
        {
            return "Nothing was recorded: " + string.Join(" ", errors);
        }

        var session = services.GetRequiredService<ISession>();
        await session.SaveChangesAsync(cancellationToken);

        var recomputed = false;

        if (ReadBool(arguments, "recomputeDay") == true)
        {
            recomputed = await adherence.SaveDayAsync(worker, date.Value, cancellationToken) is not null;

            if (recomputed)
            {
                await session.SaveChangesAsync(cancellationToken);
            }
        }

        return ToJson(new
        {
            recorded = true,
            id = exception.ItemId,
            worker = new { id = worker.ItemId, name = worker.DisplayName },
            start = InZone(exception.StartUtc, zone),
            end = InZone(exception.EndUtc, zone),
            timeZone = zone,
            reason = exception.Reason.ToString(),
            note = exception.Note,
            dayRecomputed = recomputed,
            message = recomputed
                ? "The exception was recorded and the worker's day was computed again and saved."
                : "The exception was recorded. It counts once the worker's day is saved; the history saves each day after it ends.",
        });
    }
}
