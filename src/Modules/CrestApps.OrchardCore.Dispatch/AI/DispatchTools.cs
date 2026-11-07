using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Workforce.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Dispatch.AI;

/// <summary>
/// Finds the technicians who can do a visit on a day, best first, and why the others can't.
/// </summary>
public sealed class DispatchFindTechnicianTool : WorkforceAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "dispatchFindTechnician";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "number": { "type": "string", "description": "The visit's number (V-000012) or its work order's (WO-000012)." },
            "date": { "type": "string", "description": "The day to look at, as yyyy-MM-dd in the dispatch time zone." }
          },
          "required": ["number", "date"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Lists the technicians who can do a visit on a day, best first, with the earliest start, travel and score, and the technicians ruled out with the reason (skills, certifications, territory, shifts, time off, other visits).";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, DispatchPermissions.ViewDispatch))
        {
            return "You can't see the dispatch board.";
        }

        var visit = await FieldServiceLookups.VisitAsync(services, Read(arguments, "number"), cancellationToken);
        var date = ReadDate(arguments, "date");

        if (visit is null)
        {
            return $"No visit or work order is numbered '{Read(arguments, "number")}'.";
        }

        if (date is null)
        {
            return "Give the day as yyyy-MM-dd.";
        }

        var zone = await FieldServiceLookups.TimeZoneAsync(services);
        var from = CalendarTime.ToUtc(date.Value.ToDateTime(TimeOnly.MinValue), zone);
        var result = await services.GetRequiredService<IDispatchCandidateFinder>().FindAsync(visit, from, from.AddDays(1), cancellationToken: cancellationToken);

        return JsonSerializer.Serialize(new
        {
            visit = visit.Number,
            summary = visit.Summary,
            candidates = result.Candidates.Take(10).Select(candidate => new
            {
                name = candidate.Name,
                start = FieldServiceLookups.Local(candidate.StartUtc, zone),
                end = FieldServiceLookups.Local(candidate.EndUtc, zone),
                travelMinutes = candidate.TravelMinutes,
                score = Math.Round(candidate.Score, 2),
                warnings = candidate.Warnings,
            }),
            ruledOut = result.Excluded.Select(item => new { name = item.Name, reason = item.Reason }),
            message = result.Message,
            timeZone = zone,
        }, JsonOptions);
    }
}

/// <summary>
/// Proposes a better day and explains it; applies the moves the dispatcher approves.
/// </summary>
public sealed class DispatchOptimizeTool : WorkforceAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "dispatchOptimizeAndExplain";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "date": { "type": "string", "description": "The day, as yyyy-MM-dd in the dispatch time zone." },
            "includeUnscheduled": { "type": "boolean", "description": "Also fit in visits waiting for a time." },
            "apply": { "type": "array", "items": { "type": "string" }, "description": "Only after the dispatcher approved them: the visit numbers of the proposed moves to apply." }
          },
          "required": ["date"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Proposes a better dispatch day: each move (visit, from whom and when, to whom and when), the travel before and after, and the visits that fit nobody. Explain the proposal; apply moves only when the dispatcher says so, by calling again with their visit numbers in apply.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, DispatchPermissions.ManageDispatch))
        {
            return "You can't dispatch visits.";
        }

        var date = ReadDate(arguments, "date");

        if (date is null)
        {
            return "Give the day as yyyy-MM-dd.";
        }

        var optimization = services.GetRequiredService<IDispatchOptimizationService>();
        var proposal = await optimization.ProposeAsync(date.Value, null, ReadBool(arguments, "includeUnscheduled"), cancellationToken);
        var zone = await FieldServiceLookups.TimeZoneAsync(services);
        var apply = ReadList(arguments, "apply");

        if (apply.Count > 0)
        {
            var chosen = proposal.Moves.Where(move => apply.Contains(move.Number, StringComparer.OrdinalIgnoreCase)).ToList();
            var applied = await optimization.ApplyAsync(chosen, user.Identity?.Name, cancellationToken);

            await services.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

            return JsonSerializer.Serialize(new { applied = applied.Applied, refused = applied.Errors.Select(pair => new { visit = pair.Key, reason = pair.Value }), notInProposal = apply.Except(chosen.Select(move => move.Number), StringComparer.OrdinalIgnoreCase) }, JsonOptions);
        }

        var moves = new List<object>();

        foreach (var move in proposal.Moves)
        {
            moves.Add(new
            {
                visit = move.Number,
                summary = move.Summary,
                from = move.FromWorkerId is null ? null : await FieldServiceLookups.NamesAsync(services, [move.FromWorkerId], cancellationToken),
                fromStart = FieldServiceLookups.Local(move.FromStartUtc, zone),
                to = await FieldServiceLookups.NamesAsync(services, [move.ToWorkerId], cancellationToken),
                toStart = FieldServiceLookups.Local(move.ToStartUtc, zone),
            });
        }

        return JsonSerializer.Serialize(new { moves, travelBefore = proposal.TravelBefore, travelAfter = proposal.TravelAfter, fitNobody = proposal.Unassigned, solver = proposal.Solver, timeZone = zone }, JsonOptions);
    }

    private static bool ReadBool(AIFunctionArguments arguments, string name)
        => bool.TryParse(Read(arguments, name), out var value) && value;

    private static List<string> ReadList(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is not JsonElement { ValueKind: JsonValueKind.Array } array)
        {
            return [];
        }

        return [.. array.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString().Trim()).Where(item => item.Length > 0)];
    }
}

/// <summary>
/// Gives a visit to a technician at a time, with the same checks as the board.
/// </summary>
public sealed class DispatchRescheduleVisitTool : WorkforceAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "dispatchRescheduleVisit";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "number": { "type": "string", "description": "The visit's number (V-000012) or its work order's (WO-000012)." },
            "technician": { "type": "string", "description": "The technician's name. Leave out to keep the visit's technicians." },
            "start": { "type": "string", "description": "The new start, as yyyy-MM-ddTHH:mm in the dispatch time zone." }
          },
          "required": ["number", "start"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Moves a visit to another time, or gives it to another technician, after the dispatcher confirmed it. Refused, with the reason, when the technician isn't free, on shift or able to get there.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, DispatchPermissions.ManageDispatch))
        {
            return "You can't dispatch visits.";
        }

        var visit = await FieldServiceLookups.VisitAsync(services, Read(arguments, "number"), cancellationToken);
        var start = ReadDateTime(arguments, "start");

        if (visit is null)
        {
            return $"No visit or work order is numbered '{Read(arguments, "number")}'.";
        }

        if (start is null)
        {
            return "Give the new start as yyyy-MM-ddTHH:mm.";
        }

        var workerIds = visit.WorkerIds.ToList();

        if (Read(arguments, "technician") is { } name)
        {
            var worker = await FieldServiceLookups.WorkerAsync(services, name, cancellationToken);

            if (worker is null)
            {
                return $"No technician is called '{name}'.";
            }

            workerIds = [worker.ItemId, .. visit.WorkerIds.Skip(1).Where(id => id != worker.ItemId)];
        }

        if (workerIds.Count == 0)
        {
            return "Say which technician should do it.";
        }

        var zone = await FieldServiceLookups.TimeZoneAsync(services);
        var result = await services.GetRequiredService<IDispatchService>().AssignAsync(visit.ItemId, new VisitAssignment { WorkerIds = workerIds, StartUtc = CalendarTime.ToUtc(start.Value, zone) }, user.Identity?.Name, cancellationToken);

        if (result.Errors.Count > 0)
        {
            return JsonSerializer.Serialize(new { refused = string.Join(" ", result.Errors) }, JsonOptions);
        }

        await services.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            visit = result.Visit.Number,
            technicians = await FieldServiceLookups.NamesAsync(services, result.Visit.WorkerIds, cancellationToken),
            start = FieldServiceLookups.Local(result.Visit.StartUtc, zone),
            end = FieldServiceLookups.Local(result.Visit.EndUtc, zone),
            arrivalWindow = new { from = FieldServiceLookups.Local(result.Visit.ArrivalStartUtc, zone), to = FieldServiceLookups.Local(result.Visit.ArrivalEndUtc, zone) },
        }, JsonOptions);
    }
}
