using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Says whether routing and the dialer are running for each queue and campaign, when routing last decided, and which of
/// them have work due but nothing happening.
/// </summary>
internal sealed class DispatchDiagnosticsTool : ContactCenterAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "contactCenterDispatchDiagnostics";

    private const int MaxQueuesEvaluated = 25;

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "campaignOrQueue": { "type": "string", "description": "Optional. Only this queue or campaign, by name or id." },
            "includeHealthy": { "type": "boolean", "description": "Also list queues that have work and nothing wrong. Default false: only queues with a problem are listed." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Diagnoses whether routing and the dialer are working for each contact center queue and campaign: whether the queue is off or closed, whether the paced dialer is stalled, who is signed in and available, what routing last decided and when, and which queues have work due but nothing happening (quiet queues). Returns findings and a verdict per queue. Answer only from the result.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var filterText = Read(arguments, "campaignOrQueue");
        var includeHealthy = string.Equals(Read(arguments, "includeHealthy"), "true", StringComparison.OrdinalIgnoreCase);
        var names = await ContactCenterLookups.ReadQueueNamesAsync(services, cancellationToken);
        var candidateIds = filterText is null
            ? [.. names.Keys]
            : await ContactCenterLookups.FindQueueIdsAsync(services, filterText, cancellationToken);

        if (filterText is not null && candidateIds.Count == 0)
        {
            return Error($"No queue or campaign matches '{filterText}'. Use the configuration tool to list them.");
        }

        var overseen = await ContactCenterLookups.OverseenAsync(services, user, candidateIds, cancellationToken);
        var authorized = candidateIds.Where(overseen.Contains).OrderBy(id => names.GetValueOrDefault(id) ?? id, StringComparer.OrdinalIgnoreCase).ToArray();

        if (authorized.Length == 0)
        {
            return Error("You don't oversee any queue or campaign that matches, so you can't see how routing is working for them.");
        }

        // One pass reads every campaign's pending records, so the number evaluated is capped and the rest are named.
        var evaluate = authorized.Take(MaxQueuesEvaluated).ToArray();
        var notEvaluated = authorized.Skip(MaxQueuesEvaluated).Select(id => names.GetValueOrDefault(id) ?? id).ToArray();
        var now = services.GetRequiredService<IClock>().UtcNow;
        var diagnoses = await services.GetRequiredService<IDialerDispatchDiagnosticsService>().DiagnoseAsync(evaluate, cancellationToken);
        var lastDecisions = await services.GetRequiredService<IDialerDecisionLog>().GetLastRoutingDecisionTimesAsync(evaluate, cancellationToken);
        var agents = (await services.GetRequiredService<IAgentProfileManager>().GetAllAsync(cancellationToken) ?? []).ToArray();
        var pacing = new Dictionary<string, DialerPacingQueueHealth>(StringComparer.OrdinalIgnoreCase);

        if (services.GetService<IDialerPacingHealthService>() is { } healthService)
        {
            foreach (var queue in (await healthService.EvaluateAsync(cancellationToken)).Queues)
            {
                pacing.TryAdd(queue.QueueId, queue);
            }
        }

        var results = new List<(string Name, InventoryAnalysis Analysis, bool Quiet, bool Problem, object Result)>();

        foreach (var diagnosis in diagnoses)
        {
            // Nothing pending and nothing waiting is nothing to explain.
            if (diagnosis.PendingCount == 0 && diagnosis.WaitingItemCount == 0 && !pacing.ContainsKey(diagnosis.QueueId))
            {
                continue;
            }

            var name = names.GetValueOrDefault(diagnosis.QueueId) ?? diagnosis.QueueId;
            var (signedIn, available) = ContactCenterLookups.CountAgents(agents, diagnosis.QueueId);
            var health = pacing.GetValueOrDefault(diagnosis.QueueId);
            var analysis = DispatchAnalyzer.Analyze(new QueueDispatchFacts(
                diagnosis,
                name,
                lastDecisions.TryGetValue(diagnosis.QueueId, out var last) ? last : null,
                signedIn,
                available,
                health?.State.ToString(),
                health?.State == DialerPacingHealthState.Stalled ? (int)health.TimeWithoutCompletedCycle.TotalMinutes : null,
                health?.State == DialerPacingHealthState.LockNotAcquired ? health.LockNotAcquiredSinceUtc : null,
                now));
            var problem = analysis.Findings.Any(finding => finding.Likelihood is Likelihood.Certain or Likelihood.Likely);

            results.Add((name, analysis, DispatchAnalyzer.IsQuiet(analysis), problem, new
            {
                id = diagnosis.QueueId,
                name,
                type = diagnosis.CampaignId is null ? "queue" : "campaign",
                verdict = analysis.Verdict,
                pending = diagnosis.PendingCount,
                waiting = diagnosis.WaitingItemCount,
                reserved = diagnosis.ReservedItemCount,
                assigned = diagnosis.AssignedItemCount,
                dialableNow = diagnosis.DialableNowCount,
                heldBack = diagnosis.HeldBackCount,
                heldBackReasons = diagnosis.Buckets
                    .Where(bucket => bucket.Blocker != DispatchBlocker.DialableNow)
                    .Select(bucket => new { reason = BlockerWords.Describe(bucket.Blocker), bucket.Count, nextDueUtc = Utc(bucket.NextDueUtc) }),
                queueBlocker = diagnosis.QueueBlocker?.ToString(),
                pacing = diagnosis.CampaignId is null ? null : diagnosis.Pacing.ToString(),
                pacingState = health?.State.ToString(),
                lastRoutingDecisionUtc = Utc(last),
                signedInAgents = signedIn,
                availableAgents = available,
                findings = analysis.Findings,
            }));
        }

        var shown = results.Where(result => includeHealthy || result.Problem).ToArray();
        var quiet = results.Where(result => result.Quiet).Select(result => result.Name).ToArray();

        return new
        {
            generatedUtc = Utc(now),
            verdict = results.Count == 0
                ? "Nothing is pending or waiting in any queue or campaign in scope, so there is no work for routing or the dialer to do."
                : quiet.Length > 0
                    ? $"{quiet.Length} of {results.Count} queue(s) with work have work due but nothing happening: {string.Join(", ", quiet.Take(10))}."
                    : results.Any(result => result.Problem)
                        ? $"{results.Count(result => result.Problem)} of {results.Count} queue(s) with work have a problem."
                        : $"{results.Count} queue(s) have work and routing and the dialer are working for all of them.",
            queuesWithWork = results.Count,
            quietQueues = Limit(quiet),
            queues = Limit(shown.OrderByDescending(result => result.Quiet).ThenByDescending(result => result.Problem).Select(result => result.Result)),
            notEvaluated = notEvaluated.Length == 0 ? null : Limit(notEvaluated),
        };
    }
}
