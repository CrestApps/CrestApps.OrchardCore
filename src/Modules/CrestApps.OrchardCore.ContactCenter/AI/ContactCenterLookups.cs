using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Finds the agents, queues and campaigns a manager names in words, and decides which of them they oversee.
/// </summary>
internal static class ContactCenterLookups
{
    /// <summary>
    /// Finds an agent by user name, display name, name, agent identifier or user identifier.
    /// </summary>
    /// <param name="agents">Every agent.</param>
    /// <param name="text">What the manager called the agent.</param>
    /// <returns>The agent when exactly one matches; otherwise the agents that match, none or several.</returns>
    public static (AgentProfile Agent, IReadOnlyList<AgentProfile> Candidates) FindAgent(IEnumerable<AgentProfile> agents, string text)
    {
        ArgumentNullException.ThrowIfNull(agents);

        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, []);
        }

        var all = agents.Where(agent => agent is not null && !string.IsNullOrEmpty(agent.ItemId)).ToArray();
        var needle = text.Trim();

        var exact = all
            .Where(agent => Names(agent).Any(name => string.Equals(name, needle, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (exact.Length == 1)
        {
            return (exact[0], exact);
        }

        if (exact.Length > 1)
        {
            return (null, exact);
        }

        // A name written without spaces or case, as a manager types it, still finds the agent: "oday ayoub" or "OdayAyoub".
        var squashed = Squash(needle);
        var loose = all
            .Where(agent => Names(agent).Any(name => Squash(name).Contains(squashed, StringComparison.Ordinal)))
            .ToArray();

        return (loose.Length == 1 ? loose[0] : null, loose);
    }

    /// <summary>
    /// Finds the queues and campaign queues a manager named.
    /// </summary>
    /// <param name="services">The services of the request.</param>
    /// <param name="text">The queue's or campaign's name or identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The identifiers of the queues that match. A campaign is named by its virtual campaign queue identifier.</returns>
    public static async Task<IReadOnlyList<string>> FindQueueIdsAsync(IServiceProvider services, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var needle = text.Trim();
        var ids = new List<string>();

        foreach (var queue in await services.GetRequiredService<IActivityQueueManager>().GetAllAsync(cancellationToken) ?? [])
        {
            if (queue is not null && (string.Equals(queue.ItemId, needle, StringComparison.OrdinalIgnoreCase) ||
                (queue.Name?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)))
            {
                ids.Add(queue.ItemId);
            }
        }

        if (services.GetService<ICatalogManager<OmnichannelCampaign>>() is { } campaigns)
        {
            foreach (var campaign in await campaigns.GetAllAsync(cancellationToken) ?? [])
            {
                if (campaign is not null && (string.Equals(campaign.ItemId, needle, StringComparison.OrdinalIgnoreCase) ||
                    (campaign.DisplayText?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)))
                {
                    ids.Add(ContactCenterConstants.CampaignQueue.CreateId(campaign.ItemId));
                }
            }
        }

        return ids;
    }

    /// <summary>
    /// Reads the queues and campaign queues the signed-in person oversees, exactly as the live dashboard decides it.
    /// </summary>
    /// <param name="services">The services of the request.</param>
    /// <param name="user">The signed-in person.</param>
    /// <param name="queueIds">The queues to check.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The queues among <paramref name="queueIds"/> that the person oversees.</returns>
    public static async Task<IReadOnlySet<string>> OverseenAsync(
        IServiceProvider services,
        ClaimsPrincipal user,
        IEnumerable<string> queueIds,
        CancellationToken cancellationToken)
    {
        var overseen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return overseen;
        }

        var authorization = services.GetRequiredService<ISupervisorQueueAuthorizationService>();

        foreach (var queueId in queueIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await authorization.IsAuthorizedAsync(user, userId, queueId, cancellationToken))
            {
                overseen.Add(queueId);
            }
        }

        return overseen;
    }

    /// <summary>
    /// Reads the name of every stored queue and every campaign's virtual queue.
    /// </summary>
    /// <param name="services">The services of the request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The names by queue identifier. A campaign is named by its virtual campaign queue identifier.</returns>
    public static async Task<Dictionary<string, string>> ReadQueueNamesAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var queue in await services.GetRequiredService<IActivityQueueManager>().GetAllAsync(cancellationToken) ?? [])
        {
            if (queue is not null && !string.IsNullOrEmpty(queue.ItemId))
            {
                names[queue.ItemId] = string.IsNullOrWhiteSpace(queue.Name) ? queue.ItemId : queue.Name;
            }
        }

        // A campaign is a queue of its own that is never stored, so every campaign is a candidate: one with work pending and
        // nobody signed in is exactly the one to explain.
        if (services.GetService<ICatalogManager<OmnichannelCampaign>>() is { } campaigns)
        {
            foreach (var campaign in await campaigns.GetAllAsync(cancellationToken) ?? [])
            {
                if (campaign is not null && !string.IsNullOrEmpty(campaign.ItemId))
                {
                    names[ContactCenterConstants.CampaignQueue.CreateId(campaign.ItemId)] =
                        string.IsNullOrWhiteSpace(campaign.DisplayText) ? campaign.ItemId : campaign.DisplayText;
                }
            }
        }

        return names;
    }

    /// <summary>
    /// Counts the agents signed in to a queue or campaign and how many of them are Available.
    /// </summary>
    /// <param name="agents">Every agent.</param>
    /// <param name="queueId">The queue or campaign queue identifier.</param>
    /// <returns>How many agents are signed in and how many are Available.</returns>
    public static (int SignedIn, int Available) CountAgents(IEnumerable<AgentProfile> agents, string queueId)
    {
        var signedIn = agents
            .Where(agent => AgentAvailabilityDiagnostics.GetSignedInQueueIds(agent).Contains(queueId, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        return (signedIn.Length, signedIn.Count(agent => agent.PresenceStatus == AgentPresenceStatus.Available));
    }

    /// <summary>
    /// Gets the name to show for an agent.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <returns>Their display name, user name or name.</returns>
    public static string NameOf(AgentProfile agent)
        => !string.IsNullOrWhiteSpace(agent.DisplayName) ? agent.DisplayName : agent.UserName ?? agent.Name ?? agent.ItemId;

    private static IEnumerable<string> Names(AgentProfile agent)
        => new[] { agent.UserName, agent.DisplayName, agent.Name, agent.ItemId, agent.UserId }.Where(name => !string.IsNullOrWhiteSpace(name));

    private static string Squash(string value)
        => new([.. value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);
}
