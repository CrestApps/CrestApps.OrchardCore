using CrestApps.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using YesSql;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// What uses a desk, a status, a priority or a service-level policy, so a record still in use isn't deleted: a deleted
/// desk would hide its tickets from everyone and leave their timers running, a deleted status or priority would leave
/// tickets pointing at nothing.
/// </summary>
public sealed class TicketSettingsUsage : IServiceLevelPolicyUsageProvider
{
    private readonly ISession _session;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketSettingsUsage"/> class.
    /// </summary>
    public TicketSettingsUsage(ISession session, ICatalog<TicketDesk> desks, IContentDefinitionManager contentDefinitionManager, IStringLocalizer<TicketSettingsUsage> stringLocalizer)
    {
        _session = session;
        _desks = desks;
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets what uses a desk: its tickets and the ticket types that file new tickets under it.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetUsagesAsync(TicketDesk desk, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(desk);

        var id = desk.ItemId;
        var usages = new List<string>();
        var tickets = await _session.QueryIndex<TicketIndex>(index => index.DeskId == id).CountAsync(cancellationToken);

        if (tickets > 0)
        {
            usages.Add(S.Plural(tickets, "1 ticket", "{0} tickets"));
        }

        usages.AddRange(await TypesAsync(settings => settings.DefaultDeskId == id));

        return usages;
    }

    /// <summary>
    /// Gets what uses a status: the tickets in it.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetUsagesAsync(TicketStatus status, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(status);

        var id = status.ItemId;
        var tickets = await _session.QueryIndex<TicketIndex>(index => index.StatusId == id).CountAsync(cancellationToken);

        return tickets > 0 ? [S.Plural(tickets, "1 ticket", "{0} tickets")] : [];
    }

    /// <summary>
    /// Gets what uses a priority: the tickets that have it and the ticket types that give it to new tickets.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetUsagesAsync(TicketPriority priority, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(priority);

        var id = priority.ItemId;
        var usages = new List<string>();
        var tickets = await _session.QueryIndex<TicketIndex>(index => index.PriorityId == id).CountAsync(cancellationToken);

        if (tickets > 0)
        {
            usages.Add(S.Plural(tickets, "1 ticket", "{0} tickets"));
        }

        usages.AddRange(await TypesAsync(settings => settings.DefaultPriorityId == id));

        return usages;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetUsagesAsync(ServiceLevelPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var id = policy.ItemId;
        var usages = (await _desks.GetAllAsync(cancellationToken))
            .Where(desk => desk.ServiceLevelPolicyId == id)
            .Select(desk => S["Ticket desk: {0}", desk.Name].Value)
            .ToList();

        usages.AddRange(await TypesAsync(settings => settings.ServiceLevelPolicyId == id));

        return usages;
    }

    private async Task<IEnumerable<string>> TypesAsync(Func<TicketPartSettings, bool> uses)
        => (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.FirstOrDefault(part => part.PartDefinition.Name == TicketsConstants.PartName)?.GetSettings<TicketPartSettings>() is { } settings && uses(settings))
            .Select(type => S["Ticket type: {0}", type.DisplayName].Value);
}
