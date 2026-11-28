using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ServiceLevels.AI;

/// <summary>
/// Gets the service-level timers of one record, as its page shows them (a ticket's Service levels card): each metric's
/// target, when it started and is or was due, and how it stands. A record the user may not see, or one without timers, is
/// answered as one that does not exist, and the record's title and link are given only when its module lets the user see
/// it (those who manage policies see every timer, not every record).
/// </summary>
internal sealed class GetServiceLevelTimersTool : ServiceLevelAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getServiceLevelTimers";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "recordKind": {
          "type": "string",
          "description": "The kind of record, such as Ticket or WorkOrder."
        },
        "recordId": {
          "type": "string",
          "description": "The record's id as its own tools return it (for a ticket, the id from searchTickets, not its number)."
        },
        "activeOnly": {
          "type": "boolean",
          "description": "True for only the targets still counting (running or paused). False (the default) also returns met and stopped ones."
        }
      },
      "required": ["recordKind", "recordId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => TheName;

    /// <inheritdoc />
    public override string Description => "Gets the service-level timers of one record, such as a ticket or a work order: for each metric (first response, resolution...) its target, when it started and is or was due, and whether it is on track, at risk, past its target, paused, met, met late or stopped.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var kind = ReadText(arguments, "recordKind");
        var id = ReadText(arguments, "recordId");

        if (kind is null || id is null)
        {
            return "Give the recordKind (such as Ticket) and the recordId.";
        }

        var providers = ServiceLevelTimerVisibility.ByKind(services.GetServices<IServiceLevelTimerAccessProvider>());
        var seesAll = await ServiceLevelTimerVisibility.SeesEveryTimerAsync(services.GetRequiredService<IAuthorizationService>(), user);

        // Kinds are matched as stored; the provider's spelling wins, so "ticket" finds Ticket records.
        if (providers.Keys.FirstOrDefault(key => string.Equals(key, kind, StringComparison.OrdinalIgnoreCase)) is { } known)
        {
            kind = known;
        }

        if (!seesAll)
        {
            if (!providers.TryGetValue(kind, out var provider))
            {
                return providers.Count == 0
                    ? "The user may not see any service-level timers."
                    : $"The user may not see the service-level timers of '{kind}' records. Kinds they may see: {string.Join(", ", providers.Keys.Order(StringComparer.Ordinal))}.";
            }

            if ((await provider.GetVisibleAsync(user, [id], cancellationToken))?.Contains(id) != true)
            {
                return NotFound(kind);
            }
        }

        var subject = new RecordRef(kind, id);
        var allTimers = await services.GetRequiredService<IServiceLevelTracker>().GetTimersAsync(subject, activeOnly: false, cancellationToken);

        // A record without timers gets the answer of one that does not exist, to a policy manager too: whether a record
        // exists is its own module's to say, not the service levels'.
        if (allTimers.Count == 0)
        {
            return NotFound(kind);
        }

        IReadOnlyList<ServiceLevelTimer> timers = arguments.GetFirstValueOrDefault("activeOnly", false)
            ? [.. allTimers.Where(timer => timer.IsActive)]
            : allTimers;
        var describer = new ServiceLevelTimerDescriber(services);

        // The record's title and link only when its module lets the user see it, whoever sees the timers.
        var summaries = await describer.GetSummariesAsync(user, providers, [subject], cancellationToken);

        var now = services.GetRequiredService<IClock>().UtcNow;
        var items = new List<object>(timers.Count);

        foreach (var timer in timers.OrderByDescending(timer => timer.StartedUtc))
        {
            var (policyName, metricName, priorityName) = await describer.GetNamesAsync(timer, cancellationToken);

            items.Add(new
            {
                metric = timer.Metric,
                metricName,
                policyId = timer.PolicyId,
                policyName,
                priority = string.IsNullOrEmpty(timer.Priority) ? null : timer.Priority,
                priorityName,
                state = StateName(timer.State),
                outcome = Outcome(timer, now),
                started = await describer.ToLocalAsync(timer.StartedUtc),
                due = await describer.ToLocalAsync(timer.DueUtc),
                warnAt = await describer.ToLocalAsync(timer.WarnAtUtc),
                pausedSince = await describer.ToLocalAsync(timer.PausedSinceUtc),
                breachedAt = await describer.ToLocalAsync(timer.BreachedUtc),
                ended = await describer.ToLocalAsync(timer.EndedUtc),
                targetMinutes = ServiceLevelTimerDescriber.Minutes(timer.TargetSeconds),
                usedMinutes = ServiceLevelTimerDescriber.Minutes(timer.ElapsedSeconds),
                noBusinessHours = timer.NoBusinessHours ? true : (bool?)null,
            });
        }

        return Json(new
        {
            record = describer.DescribeRecord(subject, summaries),
            timers = items,
            note = UntrustedNote,
        });
    }

    /// <summary>
    /// How a timer stands, as the ticket page's badges say it: met, met late, stopped, paused, past its target, at risk
    /// (past its warning point) or on track.
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <param name="nowUtc">The current instant, in UTC.</param>
    /// <returns>The outcome.</returns>
    internal static string Outcome(ServiceLevelTimer timer, DateTime nowUtc)
        => timer.State switch
        {
            ServiceLevelTimerState.Completed => timer.BreachedUtc is null ? "met" : "metLate",
            ServiceLevelTimerState.Cancelled => "stopped",
            ServiceLevelTimerState.Paused => "paused",
            _ when timer.DueUtc <= nowUtc => "breached",
            _ when timer.WarnAtUtc <= nowUtc => "atRisk",
            _ => "onTrack",
        };

    private static string StateName(ServiceLevelTimerState state)
        => state switch
        {
            ServiceLevelTimerState.Paused => "paused",
            ServiceLevelTimerState.Completed => "completed",
            ServiceLevelTimerState.Cancelled => "cancelled",
            _ => "running",
        };

    private static string NotFound(string kind)
        => $"No '{kind}' record with that id that the user may see has service-level timers. Use the record's own tools to find its id.";
}
