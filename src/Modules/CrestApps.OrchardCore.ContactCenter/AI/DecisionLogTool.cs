using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Queries the dialer decision log: the routing decisions and dial suppressions, by agent, campaign, queue, activity,
/// reason and time, summed up by reason, with a drained queue called out.
/// </summary>
internal sealed class DecisionLogTool : ContactCenterAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "contactCenterDecisionLog";

    private const int DefaultHours = 24;
    private const int MaxHours = 168;
    private const int MaxRecent = 25;

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "agent": { "type": "string", "description": "Optional. Only decisions about this agent: user name, display name or id." },
            "campaign": { "type": "string", "description": "Optional. Only this campaign, by name or id." },
            "queue": { "type": "string", "description": "Optional. Only this stored queue, by name or id." },
            "activityId": { "type": "string", "description": "Optional. Only decisions about this activity (record) id." },
            "reason": { "type": "string", "description": "Optional. Only this reason, for example NoDestination, RetryCoolDown, OutsideCallingWindow, NoAgentsAvailable." },
            "kind": { "type": "string", "enum": ["routing", "suppression"], "description": "Optional. Only routing decisions or only dial suppressions." },
            "fromUtc": { "type": "string", "description": "Optional. Start of the period, ISO 8601 UTC. Default 24 hours ago." },
            "toUtc": { "type": "string", "description": "Optional. End of the period, ISO 8601 UTC. Default now." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Queries the contact center dialer decision log: routing decisions and dial suppressions, filtered by agent, campaign, queue, activity, reason and date range. Returns a summary by reason, recent decisions, and a clear call-out when many records were suppressed in seconds (for example NoDestination draining a queue). Answer only from the result.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var now = services.GetRequiredService<IClock>().UtcNow;
        var toUtc = ReadUtc(arguments, "toUtc") ?? now;
        var fromUtc = ReadUtc(arguments, "fromUtc") ?? toUtc.AddHours(-DefaultHours);

        if (fromUtc >= toUtc)
        {
            return Error("The start of the period must be before its end.");
        }

        if (toUtc - fromUtc > TimeSpan.FromHours(MaxHours))
        {
            fromUtc = toUtc.AddHours(-MaxHours);
        }

        var query = new DialerDecisionLogQuery
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            ActivityItemId = Read(arguments, "activityId"),
            Reason = Read(arguments, "reason"),
            EventType = Read(arguments, "kind")?.ToLowerInvariant() switch
            {
                "routing" => ContactCenterConstants.Events.RoutingDecisionMade,
                "suppression" => ContactCenterConstants.Events.DialSuppressed,
                _ => null,
            },
        };

        var names = await ContactCenterLookups.ReadQueueNamesAsync(services, cancellationToken);

        if (Read(arguments, "agent") is { } agentText)
        {
            var (agent, candidates) = ContactCenterLookups.FindAgent(await services.GetRequiredService<IAgentProfileManager>().GetAllAsync(cancellationToken) ?? [], agentText);

            if (agent is null)
            {
                return candidates.Count == 0
                    ? Error($"No agent matches '{agentText}'.")
                    : Error($"More than one agent matches '{agentText}': {string.Join(", ", candidates.Take(10).Select(candidate => $"{ContactCenterLookups.NameOf(candidate)} ({candidate.ItemId})"))}.");
            }

            query.AgentId = agent.ItemId;
        }

        if (Read(arguments, "campaign") is { } campaignText)
        {
            var campaigns = (await ContactCenterLookups.FindQueueIdsAsync(services, campaignText, cancellationToken))
                .Where(ContactCenterConstants.IsCampaignQueue)
                .ToArray();

            if (campaigns.Length != 1)
            {
                return campaigns.Length == 0
                    ? Error($"No campaign matches '{campaignText}'.")
                    : Error($"More than one campaign matches '{campaignText}': {string.Join(", ", campaigns.Select(id => names.GetValueOrDefault(id) ?? id))}.");
            }

            query.CampaignId = ContactCenterConstants.CampaignQueue.GetCampaignId(campaigns[0]);
        }

        if (Read(arguments, "queue") is { } queueText)
        {
            var queues = (await ContactCenterLookups.FindQueueIdsAsync(services, queueText, cancellationToken))
                .Where(id => !ContactCenterConstants.IsCampaignQueue(id))
                .ToArray();

            if (queues.Length != 1)
            {
                return queues.Length == 0
                    ? Error($"No queue matches '{queueText}'.")
                    : Error($"More than one queue matches '{queueText}': {string.Join(", ", queues.Select(id => names.GetValueOrDefault(id) ?? id))}.");
            }

            query.QueueId = queues[0];
        }

        var all = await services.GetRequiredService<IDialerDecisionLog>().GetEntriesAsync(query, cancellationToken) ?? [];

        // Only the queues and campaigns the person oversees are shown, as on the live dashboard.
        var queueIds = all
            .Select(ScopeOf)
            .Where(id => id is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var overseen = await ContactCenterLookups.OverseenAsync(services, user, queueIds, cancellationToken);
        var visible = all
            .Where(entry => ScopeOf(entry) is not { } scope || overseen.Contains(scope))
            .OrderByDescending(entry => entry.LastSeenUtc)
            .Take(DecisionPatterns.MaxEntries)
            .ToArray();
        var hidden = all.Count - visible.Length;

        var analysis = DecisionLogAnalyzer.Analyze(visible, names);

        return new
        {
            generatedUtc = Utc(now),
            period = new { fromUtc = Utc(fromUtc), toUtc = Utc(toUtc) },
            verdict = analysis.Verdict,
            matchingDecisions = analysis.Reasons.Sum(row => row.Decisions),
            hiddenBecauseYouDoNotOverseeTheQueue = hidden > 0 ? hidden : (int?)null,
            findings = analysis.Findings,
            byReason = Limit(analysis.Reasons.Select(row => new { row.Kind, row.Reason, row.Meaning, row.Decisions, row.Events, lastSeenUtc = Utc(row.LastSeenUtc) })),
            byCampaignOrQueue = Limit(visible
                .GroupBy(entry => ScopeOf(entry) ?? string.Empty)
                .Select(group => new { id = group.Key, name = names.GetValueOrDefault(group.Key), decisions = group.Sum(entry => Math.Max(1, entry.OccurrenceCount)) })
                .OrderByDescending(group => group.decisions)),
            recent = Limit(
                visible.Take(MaxRecent).Select(entry => new
                {
                    kind = entry.EventType == ContactCenterConstants.Events.DialSuppressed ? "Suppression" : "Routing",
                    entry.Reason,
                    activityId = entry.ActivityItemId,
                    campaignId = entry.CampaignId,
                    queueId = entry.QueueId,
                    agentId = entry.AgentId,
                    times = Math.Max(1, entry.OccurrenceCount),
                    firstUtc = Utc(entry.OccurredUtc),
                    lastUtc = Utc(entry.LastSeenUtc),
                }),
                MaxRecent),
            readLimit = all.Count > DecisionPatterns.MaxEntries ? $"Only the newest {DecisionPatterns.MaxEntries} entries were read; narrow the period or filters for the rest." : null,
        };
    }

    private static string ScopeOf(Core.Indexes.DialerDecisionIndex entry)
        => !string.IsNullOrEmpty(entry.CampaignId)
            ? ContactCenterConstants.CampaignQueue.CreateId(entry.CampaignId)
            : string.IsNullOrEmpty(entry.QueueId) ? null : entry.QueueId;
}
