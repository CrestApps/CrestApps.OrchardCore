using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Aggregates a period's interactions into the agent productivity report. A call counts as one an agent handled only
/// when the shared <see cref="InteractionOutcomeClassifier"/> says an agent answered it, so a call the platform
/// answered itself to record a voicemail is nobody's handled call and none of its time is anybody's talk time.
/// </summary>
internal static class AgentProductivityBuilder
{
    /// <summary>
    /// Builds the agent productivity report.
    /// </summary>
    /// <param name="fromUtc">The start of the period.</param>
    /// <param name="toUtc">The end of the period.</param>
    /// <param name="interactions">The period's interactions.</param>
    /// <param name="completedByUser">The CRM activities each user completed in the period.</param>
    /// <param name="agents">The agents to report on.</param>
    /// <param name="outcomes">The classifier that decides each interaction's outcome.</param>
    /// <returns>The report.</returns>
    public static AgentProductivityReport Build(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<Interaction> interactions,
        IReadOnlyDictionary<string, long> completedByUser,
        IReadOnlyList<AgentProfile> agents,
        InteractionOutcomeClassifier outcomes)
    {
        var stats = new Dictionary<string, AgentProductivityRow>(StringComparer.Ordinal);

        foreach (var interaction in interactions)
        {
            if (string.IsNullOrEmpty(interaction.AgentId) || !outcomes.IsAnswered(interaction))
            {
                continue;
            }

            if (!stats.TryGetValue(interaction.AgentId, out var row))
            {
                row = new AgentProductivityRow { AgentId = interaction.AgentId };
                stats[interaction.AgentId] = row;
            }

            row.InteractionsHandled++;

            if (interaction.Direction == InteractionDirection.Inbound)
            {
                row.InboundHandled++;
            }
            else
            {
                row.OutboundHandled++;
            }

            row.TotalTalkTimeSeconds += outcomes.GetTalkSeconds(interaction);
            row.TotalWrapUpTimeSeconds += CallInsightsBuilder.GetWrapUpSeconds(interaction);
        }

        foreach (var agent in agents)
        {
            var completed = !string.IsNullOrEmpty(agent.UserId) && completedByUser.TryGetValue(agent.UserId, out var count)
                ? count
                : 0L;

            stats.TryGetValue(agent.ItemId, out var row);

            if (row is null && completed == 0)
            {
                continue;
            }

            row ??= new AgentProductivityRow { AgentId = agent.ItemId };
            row.UserName = agent.UserName;
            row.DisplayName = ResolveAgentName(agent);
            row.ActivitiesCompleted = completed;

            stats[agent.ItemId] = row;
        }

        foreach (var row in stats.Values)
        {
            if (string.IsNullOrEmpty(row.DisplayName))
            {
                row.DisplayName = row.AgentId;
            }

            row.AverageWrapUpTimeSeconds = row.InteractionsHandled > 0 ? row.TotalWrapUpTimeSeconds / row.InteractionsHandled : 0d;
            row.AverageHandleTimeSeconds = row.InteractionsHandled > 0
                ? (row.TotalTalkTimeSeconds + row.TotalWrapUpTimeSeconds) / row.InteractionsHandled
                : 0d;
        }

        return new AgentProductivityReport
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Rows = stats.Values
                .OrderByDescending(row => row.InteractionsHandled)
                .ThenByDescending(row => row.ActivitiesCompleted)
                .ToList(),
        };
    }

    private static string ResolveAgentName(AgentProfile agent)
    {
        if (!string.IsNullOrWhiteSpace(agent.DisplayName))
        {
            return agent.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(agent.UserName))
        {
            return agent.UserName;
        }

        return agent.ItemId;
    }
}
