using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Reports.DataSources;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Reports;

/// <summary>
/// One record of a messaging report data set: a stored document and the agent profile it names.
/// </summary>
/// <typeparam name="TDocument">The stored document type.</typeparam>
public sealed class MessagingReportRecord<TDocument>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingReportRecord{TDocument}"/> class.
    /// </summary>
    /// <param name="document">The stored document.</param>
    /// <param name="agent">The agent profile the document names, when it was asked for and still exists.</param>
    public MessagingReportRecord(TDocument document, AgentProfile agent)
    {
        Document = document;
        Agent = agent;
    }

    /// <summary>
    /// Gets the stored document.
    /// </summary>
    public TDocument Document { get; }

    /// <summary>
    /// Gets the agent profile the document names, or <see langword="null"/>.
    /// </summary>
    public AgentProfile Agent { get; }

    /// <summary>
    /// Gets the name of the agent: the display name of the agent profile, or its user name.
    /// </summary>
    public string AgentName => Agent is null
        ? null
        : string.IsNullOrWhiteSpace(Agent.DisplayName) ? Agent.UserName : Agent.DisplayName;
}

/// <summary>
/// Resolves the agent profiles the messaging data sets name.
/// </summary>
internal static class MessagingReportAgents
{
    /// <summary>
    /// Loads the agent profiles with the given identifiers when the query uses one of the agent fields.
    /// </summary>
    /// <param name="agentProfileStore">The agent profile store.</param>
    /// <param name="query">The query.</param>
    /// <param name="agentIds">The agent profile identifiers the loaded documents name.</param>
    /// <param name="agentFields">The fields that read the agent profile.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The agent profiles keyed by identifier, empty when the query does not use them.</returns>
    public static async Task<IReadOnlyDictionary<string, AgentProfile>> LoadAsync(
        IAgentProfileStore agentProfileStore,
        ReportDataSourceQuery query,
        IEnumerable<string> agentIds,
        IEnumerable<string> agentFields,
        CancellationToken cancellationToken)
    {
        var agents = new Dictionary<string, AgentProfile>(StringComparer.Ordinal);

        if (query.Fields is not null && query.Fields.Count > 0 && !agentFields.Any(query.Fields.Contains))
        {
            return agents;
        }

        var ids = agentIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (ids.Length == 0)
        {
            return agents;
        }

        foreach (var agent in await agentProfileStore.GetAsync(ids, cancellationToken) ?? [])
        {
            if (!string.IsNullOrEmpty(agent?.ItemId))
            {
                agents[agent.ItemId] = agent;
            }
        }

        return agents;
    }

    /// <summary>
    /// Gets an agent profile.
    /// </summary>
    /// <param name="agents">The loaded agent profiles.</param>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <returns>The agent profile, or <see langword="null"/>.</returns>
    public static AgentProfile Find(IReadOnlyDictionary<string, AgentProfile> agents, string agentId)
    {
        return agentId is not null && agents.TryGetValue(agentId, out var agent) ? agent : null;
    }
}
