using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// One agent on the roster.
/// </summary>
/// <param name="Name">The agent's name.</param>
/// <param name="Presence">Their presence.</param>
/// <param name="SessionLive">Whether they have a live session.</param>
/// <param name="SignedInAnywhere">Whether they are signed in to any queue or campaign.</param>
/// <param name="ActiveInteractions">How many calls they hold.</param>
/// <param name="Capacity">How many they may hold at once.</param>
/// <param name="HoldsReservation">Whether an offer is open for them.</param>
/// <param name="Stale">Whether a stale state is flagged on them.</param>
internal sealed record RosterEntry(string Name, AgentPresenceStatus Presence, bool SessionLive, bool SignedInAnywhere, int ActiveInteractions, int Capacity, bool HoldsReservation, bool Stale);

/// <summary>
/// Sums up the roster in a sentence.
/// </summary>
internal static class RosterSummary
{
    /// <summary>
    /// Describes who is signed in and who will actually get work.
    /// </summary>
    /// <param name="entries">The agents.</param>
    /// <returns>The verdict.</returns>
    public static string Describe(IReadOnlyCollection<RosterEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return "No agent matches.";
        }

        var signedIn = entries.Where(entry => entry.SignedInAnywhere).ToArray();
        var available = signedIn.Where(entry => entry.Presence == AgentPresenceStatus.Available).ToArray();
        var reachable = available.Where(entry => entry.SessionLive && entry.ActiveInteractions < entry.Capacity && !entry.HoldsReservation).ToArray();
        var parts = new List<string>
        {
            $"{signedIn.Length} of {entries.Count} agent(s) are signed in to a queue or campaign; {available.Length} of them are Available and {reachable.Length} can be offered work right now.",
        };

        var noSession = available.Count(entry => !entry.SessionLive);

        if (noSession > 0)
        {
            parts.Add($"{noSession} Available agent(s) have no live session, so routing passes them over.");
        }

        var held = signedIn.Count(entry => entry.HoldsReservation);

        if (held > 0)
        {
            parts.Add($"{held} agent(s) hold an open offer.");
        }

        var stale = entries.Count(entry => entry.Stale);

        if (stale > 0)
        {
            parts.Add($"{stale} agent(s) have a stale state.");
        }

        return string.Join(" ", parts);
    }
}

/// <summary>
/// Lists who is signed in where, with their status, capacity and current work.
/// </summary>
internal sealed class AgentRosterTool : ContactCenterAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "contactCenterAgentRoster";

    private const int MaxAgents = 30;

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "campaignOrQueue": { "type": "string", "description": "Optional. Only agents signed in to this queue or campaign, by name or id." },
            "presence": { "type": "string", "description": "Optional. Only agents with this presence: Available, Busy, Reserved, Away, Offline and so on." },
            "signedInOnly": { "type": "boolean", "description": "Only agents signed in to a queue or campaign. Default true." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Lists the contact center agents: who is signed in to which queues and campaigns, their presence, whether their session is live, how many calls they hold against their capacity, any open offer and the work they are on, and any stale state. Says how many can be offered work right now. Answer only from the result.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var queueText = Read(arguments, "campaignOrQueue");
        var presenceText = Read(arguments, "presence");
        var signedInOnly = !string.Equals(Read(arguments, "signedInOnly"), "false", StringComparison.OrdinalIgnoreCase);
        AgentPresenceStatus? presence = null;

        if (presenceText is not null)
        {
            if (!Enum.TryParse<AgentPresenceStatus>(presenceText, ignoreCase: true, out var parsed))
            {
                return Error($"'{presenceText}' is not a presence. Use one of: {string.Join(", ", Enum.GetNames<AgentPresenceStatus>())}.");
            }

            presence = parsed;
        }

        var allAgents = (await services.GetRequiredService<IAgentProfileManager>().GetAllAsync(cancellationToken) ?? [])
            .Where(agent => agent is not null && !string.IsNullOrEmpty(agent.ItemId))
            .ToArray();
        IReadOnlyList<string> filterQueues = null;

        if (queueText is not null)
        {
            filterQueues = await ContactCenterLookups.FindQueueIdsAsync(services, queueText, cancellationToken);

            if (filterQueues.Count == 0)
            {
                return Error($"No queue or campaign matches '{queueText}'.");
            }
        }

        var overseen = await ContactCenterLookups.OverseenAsync(
            services,
            user,
            allAgents.SelectMany(AgentAvailabilityDiagnostics.GetQueueIds).Distinct(StringComparer.OrdinalIgnoreCase),
            cancellationToken);

        // An agent is shown to a supervisor who oversees a queue they work in, or when they work in none, which is itself
        // something a supervisor needs to see.
        var scoped = allAgents
            .Where(agent =>
            {
                var queues = AgentAvailabilityDiagnostics.GetQueueIds(agent);
                var signedInQueues = AgentAvailabilityDiagnostics.GetSignedInQueueIds(agent).ToArray();

                return (queues.Count == 0 ? !signedInOnly : queues.Any(overseen.Contains)) &&
                    (!signedInOnly || signedInQueues.Length > 0) &&
                    (filterQueues is null || signedInQueues.Any(queueId => filterQueues.Contains(queueId, StringComparer.OrdinalIgnoreCase))) &&
                    (presence is null || agent.PresenceStatus == presence);
            })
            .OrderBy(agent => agent.PresenceStatus)
            .ThenBy(agent => ContactCenterLookups.NameOf(agent), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var total = scoped.Length;
        scoped = [.. scoped.Take(MaxAgents)];

        var reading = await services.GetRequiredService<AgentAvailabilityReader>().ReadAsync(scoped, overseen, cancellationToken);
        var interactions = scoped.Length == 0
            ? []
            : await services.GetRequiredService<IInteractionManager>().GetActiveByAgentIdsAsync([.. scoped.Select(agent => agent.ItemId)], cancellationToken);
        var work = interactions.Where(interaction => !string.IsNullOrEmpty(interaction.AgentId)).ToLookup(interaction => interaction.AgentId, StringComparer.Ordinal);
        var entries = new List<RosterEntry>();
        var rows = new List<object>();

        foreach (var diagnosis in reading.Diagnoses)
        {
            var agent = diagnosis.Agent;
            var capacity = AgentRoutingEligibility.GetCapacity(agent);
            var active = work[agent.ItemId].ToArray();
            var holds = !string.IsNullOrEmpty(agent.ActiveReservationId);
            var stale = diagnosis.Flags.Any(flag => flag.Kind is not (AgentAvailabilityFlagKind.HoldsReservation or AgentAvailabilityFlagKind.StaleWorkNotChecked));

            entries.Add(new RosterEntry(ContactCenterLookups.NameOf(agent), agent.PresenceStatus, diagnosis.SessionLive, diagnosis.SignedInAnywhere, active.Length, capacity, holds, stale));
            rows.Add(new
            {
                id = agent.ItemId,
                name = ContactCenterLookups.NameOf(agent),
                userName = agent.UserName,
                presence = agent.PresenceStatus.ToString(),
                presenceReason = agent.PresenceReason,
                presenceSinceUtc = Utc(agent.PresenceChangedUtc),
                sessionLive = diagnosis.SessionLive,
                lastHeartbeatUtc = Utc(diagnosis.LastHeartbeatUtc),
                signedInQueues = diagnosis.SignedInQueues,
                signedInCampaigns = diagnosis.SignedInCampaigns,
                capacity,
                activeInteractions = active.Length,
                currentWork = active.Select(interaction => new { interactionId = interaction.ItemId, activityId = interaction.ActivityItemId, status = interaction.Status.ToString(), startedUtc = Utc(interaction.StartedUtc ?? interaction.CreatedUtc) }),
                holdsOpenOffer = holds,
                callsToday = diagnosis.CallsToday,
                lastOfferedUtc = Utc(agent.LastAssignedUtc),
                staleState = diagnosis.Flags.Where(flag => flag.Kind is not (AgentAvailabilityFlagKind.HoldsReservation or AgentAvailabilityFlagKind.StaleWorkNotChecked)).Select(flag => flag.Kind.ToString()),
            });
        }

        return new
        {
            generatedUtc = Utc(reading.NowUtc),
            verdict = (entries.Count == 0 && signedInOnly && presence is null && queueText is null
                ? "No agent is signed in to a queue or campaign right now. Ask again with signedInOnly false to list every agent."
                : RosterSummary.Describe(entries)) + (total > scoped.Length ? $" Only the first {scoped.Length} of {total} agents are listed; narrow the question." : string.Empty),
            totalAgents = total,
            byPresence = entries.GroupBy(entry => entry.Presence).OrderByDescending(group => group.Count()).Select(group => new { presence = group.Key.ToString(), count = group.Count() }),
            agents = Limit(rows, MaxAgents),
        };
    }
}
