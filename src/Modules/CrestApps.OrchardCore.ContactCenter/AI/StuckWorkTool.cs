using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// A reservation that outlived its expiry.
/// </summary>
/// <param name="ReservationId">The reservation's identifier.</param>
/// <param name="ActivityId">The activity it holds.</param>
/// <param name="QueueId">The queue it came from.</param>
/// <param name="AgentId">The agent it was offered to.</param>
/// <param name="ExpiresUtc">When it expired.</param>
/// <param name="QueueItemStatus">The status of the activity's queue item, or <see langword="null"/> when it has none.</param>
internal sealed record StuckReservation(string ReservationId, string ActivityId, string QueueId, string AgentId, DateTime ExpiresUtc, string QueueItemStatus);

/// <summary>
/// A queue that has work and is not being dialed or assigned.
/// </summary>
/// <param name="QueueId">The queue's identifier.</param>
/// <param name="Name">The queue's name.</param>
/// <param name="Cause">What stops it: Pacing.Stalled, Pacing.LockNotAcquired or HeadItemHoldsReservation.</param>
/// <param name="Detail">The cause in words.</param>
internal sealed record StalledQueue(string QueueId, string Name, string Cause, string Detail);

/// <summary>
/// An agent whose state nothing live backs.
/// </summary>
/// <param name="AgentId">The agent's identifier.</param>
/// <param name="Name">The agent's name.</param>
/// <param name="Reasons">Why the state is stale.</param>
internal sealed record StaleAgent(string AgentId, string Name, IReadOnlyList<string> Reasons);

/// <summary>
/// Everything read about stuck work.
/// </summary>
/// <param name="ExpiredReservations">The reservations that outlived their expiry.</param>
/// <param name="OrphanedActivities">The activities stranded with nothing live behind them.</param>
/// <param name="StalledQueues">The queues with work that are not moving.</param>
/// <param name="StaleAgents">The agents whose state nothing live backs.</param>
/// <param name="OrphanGrace">How long an activity must have been stranded to be listed.</param>
/// <param name="NowUtc">The time it was read.</param>
internal sealed record StuckWorkFacts(
    IReadOnlyList<StuckReservation> ExpiredReservations,
    IReadOnlyList<OrphanedActivity> OrphanedActivities,
    IReadOnlyList<StalledQueue> StalledQueues,
    IReadOnlyList<StaleAgent> StaleAgents,
    TimeSpan OrphanGrace,
    DateTime NowUtc);

/// <summary>
/// Says what is stuck: reservations nobody released, activities stranded in a status nothing backs, queues that stopped and
/// agents with stale state.
/// </summary>
internal static class StuckWorkAnalyzer
{
    /// <summary>
    /// Analyzes what was read.
    /// </summary>
    /// <param name="facts">What was read.</param>
    /// <returns>The verdict and findings.</returns>
    public static InventoryAnalysis Analyze(StuckWorkFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var findings = new List<ToolFinding>();

        foreach (var queue in facts.StalledQueues)
        {
            findings.Add(new ToolFinding(
                $"StalledQueue.{queue.Cause}",
                Likelihood.Certain,
                $"{queue.Name} has work but is not moving: {queue.Detail}",
                queue));
        }

        var blocking = facts.ExpiredReservations.Where(reservation => reservation.QueueItemStatus == nameof(QueueItemStatus.Waiting)).ToArray();

        if (blocking.Length > 0)
        {
            findings.Add(new ToolFinding(
                "ReservationBlocksWaitingItem",
                Likelihood.Certain,
                $"{blocking.Length} expired reservation(s) still sit on activities whose queue item is waiting. A reservation that was never released holds the activity's claim, so the queue cannot reserve that item again and, when it is the head of the queue, nothing behind it is dialed. The next attempt to reserve the item cancels such a reservation.",
                new { activities = blocking.Take(10).Select(reservation => new { reservation.ActivityId, reservation.ReservationId, reservation.QueueId, expiredUtc = ContactCenterAIToolBase.Utc(reservation.ExpiresUtc) }) }));
        }

        var others = facts.ExpiredReservations.Except(blocking).ToArray();

        if (others.Length > 0)
        {
            findings.Add(new ToolFinding(
                "ExpiredReservationNotReleased",
                Likelihood.Likely,
                $"{others.Length} reservation(s) expired but were never released, so the agent they were offered to still looks reserved and the activity looks taken. The expiry sweep normally releases them within a minute; one that lingers means the sweep is not running or failed.",
                new { reservations = others.Take(10).Select(reservation => new { reservation.ActivityId, reservation.ReservationId, reservation.AgentId, expiredUtc = ContactCenterAIToolBase.Utc(reservation.ExpiresUtc) }) }));
        }

        if (facts.OrphanedActivities.Count > 0)
        {
            var requeue = facts.OrphanedActivities.Count(activity => activity.WouldRequeue);

            findings.Add(new ToolFinding(
                "OrphanedActivities",
                Likelihood.Likely,
                $"{facts.OrphanedActivities.Count} activity(ies) are stranded in an in-progress status with no live reservation or call behind them for over {facts.OrphanGrace.TotalMinutes:0} minutes. They inflate the campaign's in-progress count and are never offered again. " +
                    $"Recovery puts {requeue} back in the queue (never answered) and fails {facts.OrphanedActivities.Count - requeue} (they may have reached the customer, so they are not dialed again).",
                new { activities = facts.OrphanedActivities.Take(10) }));
        }

        if (facts.StaleAgents.Count > 0)
        {
            findings.Add(new ToolFinding(
                "StaleAgentState",
                Likelihood.Likely,
                $"{facts.StaleAgents.Count} agent(s) have a state nothing live backs: {string.Join("; ", facts.StaleAgents.Take(5).Select(agent => $"{agent.Name} ({string.Join(", ", agent.Reasons)})"))}. They show as busy or reserved but are not on a call, so they receive no work.",
                new { agents = facts.StaleAgents.Take(10) }));
        }

        var ordered = ToolFinding.Order(findings);
        var stuck = facts.StalledQueues.Count + facts.ExpiredReservations.Count + facts.OrphanedActivities.Count + facts.StaleAgents.Count;

        return new InventoryAnalysis(
            stuck == 0
                ? "Nothing is stuck: no expired reservation is waiting to be released, no activity is stranded, no queue with work has stopped and no agent has a stale state."
                : $"Found stuck work. {ordered[0].Summary}",
            ordered);
    }
}

/// <summary>
/// Lists stuck work without changing it: reservations nobody released, activities stranded with nothing live behind them,
/// queues that stopped and agents with stale state.
/// </summary>
internal sealed class StuckWorkTool : ContactCenterAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "contactCenterStuckWork";

    private const int MaxExamined = 100;
    private const int MaxQueuesChecked = 25;

    private static readonly TimeSpan _expiredGrace = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan _orphanGrace = TimeSpan.FromMinutes(10);

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Finds stuck contact center work without changing anything: reservations that expired but were never released (including one blocking a waiting queue item), activities stranded in an in-progress status with nothing live behind them, queues with work whose dialer stalled or cannot take its pacing lock, and agents whose busy or reserved state nothing live backs. Answer only from the result; it does not fix anything.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var now = services.GetRequiredService<IClock>().UtcNow;
        var names = await ContactCenterLookups.ReadQueueNamesAsync(services, cancellationToken);
        var overseen = await ContactCenterLookups.OverseenAsync(services, user, names.Keys, cancellationToken);
        var queueItems = services.GetRequiredService<IQueueItemManager>();
        var reservations = services.GetRequiredService<IActivityReservationManager>();

        var expired = new List<StuckReservation>();
        var page = await reservations.GetExpiredAsync(now - _expiredGrace, null, 0, MaxExamined, cancellationToken);

        foreach (var reservation in page.Reservations.Where(reservation => !reservation.IsResolved))
        {
            // Only the queues the person oversees are shown.
            if (!string.IsNullOrEmpty(reservation.QueueId) && !overseen.Contains(reservation.QueueId))
            {
                continue;
            }

            var item = await queueItems.FindByActivityIdAsync(reservation.ActivityItemId, cancellationToken);

            expired.Add(new StuckReservation(reservation.ItemId, reservation.ActivityItemId, reservation.QueueId, reservation.AgentId, reservation.ExpiresUtc, item?.Status.ToString()));
        }

        var orphans = (await services.GetRequiredService<IOrphanedActivityRecoveryService>().FindAsync(_orphanGrace, MaxExamined, cancellationToken))
            .Where(activity => string.IsNullOrEmpty(activity.CampaignId) || overseen.Contains(ContactCenterConstants.CampaignQueue.CreateId(activity.CampaignId)))
            .ToArray();

        var agents = (await services.GetRequiredService<IAgentProfileManager>().GetAllAsync(cancellationToken) ?? [])
            .Where(agent => AgentAvailabilityDiagnosisScope(agent, overseen))
            .ToArray();
        var stale = new List<StaleAgent>();

        if (services.GetService<IStaleAgentWorkService>() is { } staleWork && agents.Length > 0)
        {
            foreach (var (agentId, work) in await staleWork.DetectAsync(agents, cancellationToken))
            {
                if (work is { IsStale: true })
                {
                    var agent = agents.First(candidate => candidate.ItemId == agentId);

                    stale.Add(new StaleAgent(agentId, ContactCenterLookups.NameOf(agent), [.. work.Items.Select(item => item.Reason)]));
                }
            }
        }

        var stalled = new List<StalledQueue>();

        if (services.GetService<IDialerPacingHealthService>() is { } health)
        {
            foreach (var queue in (await health.EvaluateAsync(cancellationToken)).Queues.Where(queue => overseen.Contains(queue.QueueId)))
            {
                var name = names.GetValueOrDefault(queue.QueueId) ?? queue.QueueId;

                if (queue.State == DialerPacingHealthState.Stalled)
                {
                    stalled.Add(new StalledQueue(queue.QueueId, name, "Pacing.Stalled", $"no pacing cycle completed for {(int)queue.TimeWithoutCompletedCycle.TotalMinutes} minute(s) while work waits."));
                }
                else if (queue.State == DialerPacingHealthState.LockNotAcquired)
                {
                    stalled.Add(new StalledQueue(queue.QueueId, name, "Pacing.LockNotAcquired", $"every pacing cycle skipped itself since {Utc(queue.LockNotAcquiredSinceUtc)} because it cannot take the queue's pacing lock."));
                }
            }
        }

        // The item at the head of a campaign queue whose activity still holds a live reservation is what stops the queue
        // behind it: every cycle fails to reserve it.
        var campaignQueueIds = overseen.Where(ContactCenterConstants.IsCampaignQueue).Take(MaxQueuesChecked).ToArray();
        var waiting = campaignQueueIds.Length == 0 ? new Dictionary<string, int>() : await queueItems.CountWaitingByQueueIdsAsync(campaignQueueIds, cancellationToken);

        foreach (var queueId in campaignQueueIds.Where(id => waiting.GetValueOrDefault(id) > 0))
        {
            var head = await queueItems.FindNextWaitingAsync(CampaignRoutingQueue.Create(queueId), now, cancellationToken);

            if (head is null)
            {
                continue;
            }

            var held = (await reservations.GetActiveByActivityAsync(head.ActivityItemId, cancellationToken) ?? []).Where(reservation => !reservation.IsResolved).ToArray();

            if (held.Length > 0 && stalled.All(queue => queue.QueueId != queueId))
            {
                stalled.Add(new StalledQueue(queueId, names.GetValueOrDefault(queueId) ?? queueId, "HeadItemHoldsReservation", $"the item at its head (activity {head.ActivityItemId}) still holds {held.Length} reservation(s) from an offer that never finished, so the queue cannot reserve it and nothing behind it is dialed."));
            }
        }

        var analysis = StuckWorkAnalyzer.Analyze(new StuckWorkFacts(expired, orphans, stalled, stale, _orphanGrace, now));

        return new
        {
            generatedUtc = Utc(now),
            verdict = analysis.Verdict,
            findings = analysis.Findings,
            stalledQueues = Limit(stalled),
            expiredReservations = Limit(expired.Select(reservation => new
            {
                reservation.ReservationId,
                reservation.ActivityId,
                reservation.QueueId,
                reservation.AgentId,
                expiredUtc = Utc(reservation.ExpiresUtc),
                queueItemStatus = reservation.QueueItemStatus,
            })),
            orphanedActivities = Limit(orphans.Select(activity => new
            {
                activity.ActivityItemId,
                activity.CampaignId,
                activity.Status,
                reservedUtc = Utc(activity.ReservedUtc),
                recoveryWould = activity.WouldRequeue ? "put it back in the queue" : "fail it (it may have reached the customer)",
            })),
            staleAgents = Limit(stale),
            readLimit = page.HasMore ? "More expired reservations exist than were read; fix these and ask again." : null,
        };
    }

    // An agent is in scope when they work in a queue the person oversees, or in none at all.
    private static bool AgentAvailabilityDiagnosisScope(AgentProfile agent, IReadOnlySet<string> overseen)
    {
        var queues = Reports.Services.AgentAvailabilityDiagnostics.GetQueueIds(agent);

        return queues.Count == 0 || queues.Any(overseen.Contains);
    }
}
