using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Explains why an agent is not receiving calls.
/// </summary>
internal sealed class ExplainAgentNoCallsTool : ContactCenterAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "contactCenterExplainAgentNoCalls";

    private const int DefaultLookbackHours = 12;
    private const int MaxLookbackHours = 72;

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "agent": { "type": "string", "description": "The agent: user name, display name or agent id, for example 'OdayAyoub'." },
            "queueOrCampaign": { "type": "string", "description": "Optional. Only explain this queue or campaign, by name or id." },
            "lookbackHours": { "type": "integer", "description": "How many hours of offers and routing decisions to read. Default 12, at most 72." }
          },
          "required": ["agent"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Explains why a contact center agent is not receiving calls or preview offers. Checks the queues and campaigns they are signed in to, their presence, live session and capacity, any held reservation or unfinished activity that blocks new offers, what each queue would offer them right now, and their recent offers, routing decisions and suppressions. Returns findings ordered by likelihood and a verdict. Answer only from the findings.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var agentText = Read(arguments, "agent");

        if (agentText is null)
        {
            return Error("Say which agent to explain: their user name, display name or agent id.");
        }

        var agentManager = services.GetRequiredService<IAgentProfileManager>();
        var (agent, candidates) = ContactCenterLookups.FindAgent(await agentManager.GetAllAsync(cancellationToken) ?? [], agentText);

        if (agent is null)
        {
            return candidates.Count == 0
                ? Error($"No agent matches '{agentText}'. Use the agent roster tool to list the agents.")
                : Error($"More than one agent matches '{agentText}': {string.Join(", ", candidates.Take(10).Select(candidate => $"{ContactCenterLookups.NameOf(candidate)} ({candidate.ItemId})"))}. Ask again with the exact user name or id.");
        }

        var lookback = TimeSpan.FromHours(Math.Clamp(ReadInt(arguments, "lookbackHours") ?? DefaultLookbackHours, 1, MaxLookbackHours));
        var queueText = Read(arguments, "queueOrCampaign");
        var agentQueueIds = AgentAvailabilityDiagnostics.GetQueueIds(agent);
        var overseen = await ContactCenterLookups.OverseenAsync(services, user, agentQueueIds, cancellationToken);

        if (agentQueueIds.Count > 0 && overseen.Count == 0)
        {
            return Error($"{ContactCenterLookups.NameOf(agent)} works in queues and campaigns you don't oversee, so you can't see why they are not getting calls.");
        }

        string queueFilter = null;

        if (queueText is not null)
        {
            var found = await ContactCenterLookups.FindQueueIdsAsync(services, queueText, cancellationToken);

            queueFilter = found.Count == 1 ? found[0] : queueText;
        }

        var reading = await services.GetRequiredService<AgentAvailabilityReader>().ReadAsync([agent], overseen, cancellationToken);
        var diagnosis = reading.Diagnoses[0];
        var now = reading.NowUtc;
        var from = now - lookback;

        var interactions = services.GetRequiredService<IInteractionManager>();
        var open = new Dictionary<string, AgentOpenWork>(StringComparer.Ordinal);

        foreach (var interaction in await interactions.GetActiveByAgentIdsAsync([agent.ItemId], cancellationToken) ?? [])
        {
            open[interaction.ItemId] = new AgentOpenWork(interaction.ItemId, interaction.ActivityItemId, interaction.Status.ToString(), interaction.StartedUtc ?? interaction.CreatedUtc, false);
        }

        foreach (var interaction in await interactions.GetPendingWrapUpsByAgentAsync(agent.ItemId, cancellationToken) ?? [])
        {
            open[interaction.ItemId] = new AgentOpenWork(interaction.ItemId, interaction.ActivityItemId, interaction.Status.ToString(), interaction.StartedUtc ?? interaction.CreatedUtc, true);
        }

        var reservations = (await services.GetRequiredService<IActivityReservationManager>().GetActiveByAgentAsync(agent.ItemId, cancellationToken) ?? [])
            .Where(reservation => !reservation.IsResolved)
            .Select(reservation => new AgentReservation(reservation.ItemId, reservation.ActivityItemId, reservation.QueueId, reservation.Status.ToString(), reservation.CreatedUtc, reservation.ExpiresUtc))
            .ToArray();

        var offers = new List<AgentOffer>();

        foreach (var offerEvent in await services.GetRequiredService<IInteractionEventStore>()
            .GetByAggregateWindowAsync(nameof(ActivityReservation), Reports.Services.CallHandlingMetrics.OfferEventTypes, null, from, now, cancellationToken) ?? [])
        {
            var data = ReadOffer(offerEvent);

            if (data is not null && string.Equals(data.AgentId, agent.ItemId, StringComparison.Ordinal))
            {
                offers.Add(new AgentOffer(offerEvent.EventType, data.ReservationId, data.ActivityItemId, data.QueueId, data.RingSeconds, data.Reason, offerEvent.OccurredUtc));
            }
        }

        var decisions = (await services.GetRequiredService<IDialerDecisionLog>().GetEntriesAsync(
            new DialerDecisionLogQuery { AgentId = agent.ItemId, FromUtc = from },
            cancellationToken) ?? [])
            .OrderByDescending(entry => entry.LastSeenUtc)
            .Take(DecisionPatterns.MaxEntries)
            .Select(entry => new AgentDecision(
                entry.EventType == ContactCenterConstants.Events.DialSuppressed ? "Suppression" : "Routing",
                entry.Reason,
                entry.ActivityItemId,
                entry.QueueId,
                entry.OccurrenceCount,
                entry.LastSeenUtc))
            .ToArray();

        var analysis = AgentCallAnalyzer.Analyze(new AgentCallFacts(
            diagnosis,
            [.. open.Values],
            reservations,
            offers,
            decisions,
            now,
            lookback,
            queueFilter,
            overseen));

        var visibleQueues = diagnosis.Queues.Where(queue => overseen.Contains(queue.QueueId)).ToArray();

        return new
        {
            generatedUtc = Utc(now),
            verdict = analysis.Verdict,
            agent = new
            {
                id = agent.ItemId,
                userName = agent.UserName,
                displayName = agent.DisplayName,
                presence = agent.PresenceStatus.ToString(),
                presenceReason = agent.PresenceReason,
                presenceSinceUtc = Utc(agent.PresenceChangedUtc),
                maxConcurrentInteractions = agent.MaxConcurrentInteractions,
                lastOfferedUtc = Utc(agent.LastAssignedUtc),
                lastWorkCompletedUtc = Utc(agent.LastWorkCompletedUtc),
                callsToday = diagnosis.CallsToday,
            },
            session = new { live = diagnosis.SessionLive, hasSession = diagnosis.HasSession, lastHeartbeatUtc = Utc(diagnosis.LastHeartbeatUtc) },
            findings = analysis.Findings,
            queues = Limit(visibleQueues.Select(queue => new
            {
                queue.QueueId,
                queue.Name,
                type = queue.IsCampaign ? "campaign" : "queue",
                queue.Entitled,
                queue.SignedIn,
                queue.WaitingCount,
                eligibleNow = queue.Eligible,
                blocker = queue.Blocker.ToString(),
                gate = queue.Gate.ToString(),
                queue.NextInLine,
                routingReasons = queue.RoutingReasons,
            })),
            lookback = $"{lookback.TotalHours:0} hours",
        };
    }

    private static OfferLifecycleEventData ReadOffer(InteractionEvent offerEvent)
    {
        try
        {
            return offerEvent.GetData<OfferLifecycleEventData>();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
