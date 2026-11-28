using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Reads the names the setup tools show beside the IDs a record stores, and finds a record by ID or name. A lookup whose
/// feature is off answers with nothing rather than failing.
/// </summary>
internal static class ContactCenterSetupLookups
{
    /// <summary>
    /// Gets the queue names by ID.
    /// </summary>
    public static async Task<Dictionary<string, string>> GetQueueNamesAsync(IServiceProvider services, CancellationToken cancellationToken)
        => services.GetService<IActivityQueueManager>() is { } manager
            ? (await manager.GetAllAsync(cancellationToken)).Where(queue => queue?.ItemId is not null).ToDictionary(queue => queue.ItemId, queue => queue.Name, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the queue group names by ID.
    /// </summary>
    public static async Task<Dictionary<string, string>> GetQueueGroupNamesAsync(IServiceProvider services, CancellationToken cancellationToken)
        => services.GetService<IActivityQueueGroupManager>() is { } manager
            ? (await manager.GetAllAsync(cancellationToken)).Where(group => group?.ItemId is not null).ToDictionary(group => group.ItemId, group => group.Name, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the business-hours calendar names by ID.
    /// </summary>
    public static async Task<Dictionary<string, string>> GetCalendarNamesAsync(IServiceProvider services, CancellationToken cancellationToken)
        => services.GetService<INamedCatalogManager<BusinessHoursSchedule>>() is { } manager
            ? (await manager.GetAllAsync(cancellationToken)).Where(calendar => calendar?.ItemId is not null).ToDictionary(calendar => calendar.ItemId, calendar => calendar.Name, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the campaign names by ID.
    /// </summary>
    public static async Task<Dictionary<string, string>> GetCampaignNamesAsync(IServiceProvider services, CancellationToken cancellationToken)
        => services.GetService<ICatalogManager<OmnichannelCampaign>>() is { } manager
            ? (await manager.GetAllAsync(cancellationToken)).Where(campaign => campaign?.ItemId is not null).ToDictionary(campaign => campaign.ItemId, campaign => campaign.DisplayText, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the agent names by agent profile ID.
    /// </summary>
    public static async Task<Dictionary<string, string>> GetAgentNamesAsync(IServiceProvider services, CancellationToken cancellationToken)
        => services.GetService<IAgentProfileManager>() is { } manager
            ? (await manager.GetAllAsync(cancellationToken)).Where(agent => agent?.ItemId is not null).ToDictionary(agent => agent.ItemId, DescribeAgent, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the name an agent is shown by.
    /// </summary>
    public static string DescribeAgent(AgentProfile agent)
        => agent is null
            ? null
            : !string.IsNullOrWhiteSpace(agent.DisplayName) ? agent.DisplayName : agent.UserName ?? agent.Name;

    /// <summary>
    /// Returns a reference to a record as its ID and, when known, its name.
    /// </summary>
    public static object Reference(string id, IReadOnlyDictionary<string, string> names)
        => string.IsNullOrEmpty(id)
            ? null
            : new
            {
                id,
                name = names.TryGetValue(id, out var name) ? name : null,
                exists = names.ContainsKey(id),
            };

    /// <summary>
    /// Finds a record by its ID, or else by its name, ignoring case.
    /// </summary>
    public static async Task<T> FindAsync<T>(ICatalogManager<T> manager, string idOrName, Func<T, string> name, CancellationToken cancellationToken)
        where T : CrestApps.Core.Models.CatalogItem
    {
        if (string.IsNullOrWhiteSpace(idOrName))
        {
            return null;
        }

        var record = await manager.FindByIdAsync(idOrName, cancellationToken);

        if (record is not null)
        {
            return record;
        }

        var matches = (await manager.GetAllAsync(cancellationToken))
            .Where(candidate => candidate is not null && string.Equals(name(candidate), idOrName, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>
    /// Cuts a list to one page.
    /// </summary>
    public static (List<T> Items, int Total, bool Truncated) Page<T>(IReadOnlyList<T> rows, int page, int pageSize)
    {
        var items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return (items, rows.Count, page * pageSize < rows.Count);
    }
}
