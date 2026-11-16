using System.Runtime.CompilerServices;
using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tickets.Reports.DataSources;

/// <summary>
/// The tickets a report reader may see, by the same scope the ticket list applies, and the names of the desks, statuses
/// and priorities. Kept for the reader for the length of a request.
/// </summary>
internal sealed class TicketReportAccess
{
    // How many rows are checked against the scope at a time.
    private const int BatchSize = 200;

    private readonly ITicketAccessService _access;
    private readonly ISession _session;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly ICatalog<TicketStatus> _statuses;
    private readonly ICatalog<TicketPriority> _priorities;
    private readonly ITicketCategoryService _categories;
    private readonly global::OrchardCore.ContentManagement.Metadata.IContentDefinitionManager _contentDefinitionManager;

    private ClaimsPrincipal _user;
    private TicketScopedQuery _scope;
    private TicketReportNames _names;

    public TicketReportAccess(
        ITicketAccessService access,
        ISession session,
        ICatalog<TicketDesk> desks,
        ICatalog<TicketStatus> statuses,
        ICatalog<TicketPriority> priorities,
        ITicketCategoryService categories,
        global::OrchardCore.ContentManagement.Metadata.IContentDefinitionManager contentDefinitionManager)
    {
        _access = access;
        _session = session;
        _desks = desks;
        _statuses = statuses;
        _priorities = priorities;
        _categories = categories;
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Gets the reader's scope, or <see langword="null"/> when they may see no ticket.
    /// </summary>
    public async Task<TicketScopedQuery> GetScopeAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            return null;
        }

        if (!ReferenceEquals(user, _user))
        {
            _scope = await _access.ApplyScopeAsync(user, new TicketQuery(), cancellationToken);
            _user = user;
        }

        return _scope;
    }

    /// <summary>
    /// Gets the names of the desks, statuses, priorities and categories.
    /// </summary>
    public async Task<TicketReportNames> GetNamesAsync(CancellationToken cancellationToken)
        => _names ??= new TicketReportNames(
            (await _desks.GetAllAsync(cancellationToken)).ToDictionary(desk => desk.ItemId, desk => desk.Name, StringComparer.Ordinal),
            (await _statuses.GetAllAsync(cancellationToken)).ToDictionary(status => status.ItemId, status => status.Name, StringComparer.Ordinal),
            (await _priorities.GetAllAsync(cancellationToken)).ToDictionary(priority => priority.ItemId, priority => priority.Name, StringComparer.Ordinal),
            await CategoriesAsync(cancellationToken));

    // The categories of every ticket type, by id; types sharing a taxonomy list each category once.
    private async Task<Dictionary<string, TicketCategory>> CategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = new Dictionary<string, TicketCategory>(StringComparer.Ordinal);

        foreach (var type in await _contentDefinitionManager.ListTypeDefinitionsAsync())
        {
            if (type.Parts.Any(part => part.PartDefinition.Name == TicketsConstants.PartName))
            {
                foreach (var category in await _categories.ListAsync(type.Name, cancellationToken: cancellationToken))
                {
                    categories.TryAdd(category.Id, category);
                }
            }
        }

        return categories;
    }

    /// <summary>
    /// Keeps the rows of tickets in the scope, checking them against the tickets a batch at a time.
    /// </summary>
    public async IAsyncEnumerable<TRow> KeepVisibleAsync<TRow>(
        TicketScopedQuery scope,
        IAsyncEnumerable<TRow> rows,
        Func<TRow, string> ticketIdOf,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var known = new Dictionary<string, bool>(StringComparer.Ordinal);
        var buffer = new List<TRow>(BatchSize);

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            buffer.Add(row);

            if (buffer.Count < BatchSize)
            {
                continue;
            }

            foreach (var kept in await FlushAsync(scope, buffer, ticketIdOf, known, cancellationToken))
            {
                yield return kept;
            }
        }

        foreach (var kept in await FlushAsync(scope, buffer, ticketIdOf, known, cancellationToken))
        {
            yield return kept;
        }
    }

    private async Task<List<TRow>> FlushAsync<TRow>(TicketScopedQuery scope, List<TRow> buffer, Func<TRow, string> ticketIdOf, Dictionary<string, bool> known, CancellationToken cancellationToken)
    {
        var unknown = buffer.Select(ticketIdOf).Where(id => !string.IsNullOrEmpty(id) && !known.ContainsKey(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (unknown.Length > 0)
        {
            var visible = await _session.QueryIndex<TicketIndex>(TicketScopeFilter.ToPredicate(scope))
                .Where(index => index.ContentItemId.IsIn(unknown))
                .ListAsync(cancellationToken);
            var ids = visible.Select(index => index.ContentItemId).ToHashSet(StringComparer.Ordinal);

            foreach (var id in unknown)
            {
                known[id] = ids.Contains(id);
            }
        }

        var kept = buffer.Where(row => ticketIdOf(row) is { Length: > 0 } id && known.GetValueOrDefault(id)).ToList();
        buffer.Clear();

        return kept;
    }
}

/// <summary>
/// The names of the desks, statuses and priorities, by id. An id that no longer has a record reads as itself.
/// </summary>
internal sealed class TicketReportNames
{
    private readonly IReadOnlyDictionary<string, string> _desks;
    private readonly IReadOnlyDictionary<string, string> _statuses;
    private readonly IReadOnlyDictionary<string, string> _priorities;
    private readonly IReadOnlyDictionary<string, TicketCategory> _categories;

    public TicketReportNames(IReadOnlyDictionary<string, string> desks, IReadOnlyDictionary<string, string> statuses, IReadOnlyDictionary<string, string> priorities, IReadOnlyDictionary<string, TicketCategory> categories = null)
    {
        _desks = desks;
        _statuses = statuses;
        _priorities = priorities;
        _categories = categories ?? new Dictionary<string, TicketCategory>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets a category by id, or <see langword="null"/> for none or one that was removed.
    /// </summary>
    public TicketCategory Category(string id) => string.IsNullOrEmpty(id) ? null : _categories.GetValueOrDefault(id);

    public string Desk(string id) => Name(_desks, id);

    public string Status(string id) => Name(_statuses, id);

    public string Priority(string id) => Name(_priorities, id);

    private static string Name(IReadOnlyDictionary<string, string> names, string id)
        => string.IsNullOrEmpty(id) ? null : names.GetValueOrDefault(id) ?? id;
}
