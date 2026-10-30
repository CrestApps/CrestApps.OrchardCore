using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Stores calendars as YesSql documents in the <see cref="CalendarsConstants.CollectionName"/> collection.
/// </summary>
public sealed class CalendarStore : DocumentCatalog<Calendar, CalendarIndex>, ICalendarStore
{
    // Two people sharing or renaming the same calendar must not overwrite each other's changes.
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public CalendarStore(ISession session)
        : base(session)
    {
        CollectionName = CalendarsConstants.CollectionName;
    }

    /// <inheritdoc />
    public Task<Calendar> FindBySourceAsync(string source, string sourceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(sourceId))
        {
            return Task.FromResult<Calendar>(null);
        }

        return Session.Query<Calendar, CalendarIndex>(index => index.Source == source && index.SourceId == sourceId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Calendar> FindByFeedTokenAsync(string feedToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(feedToken))
        {
            return Task.FromResult<Calendar>(null);
        }

        return Session.Query<Calendar, CalendarIndex>(index => index.FeedToken == feedToken, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(int Count, IReadOnlyList<Calendar> Calendars)> PageAsync(CalendarListQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = Filter(query);
        var count = await filtered.CountAsync(cancellationToken);
        var page = await filtered.OrderBy(index => index.Name).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, await LoadedAsync(page));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Calendar>> ListAsync(CalendarListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var calendars = await Filter(query).OrderBy(index => index.Name).Take(Math.Max(1, query.Limit)).ListAsync(cancellationToken);

        return await LoadedAsync(calendars);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Calendar>> ListSharedWithAsync(string userId, IEnumerable<string> roles, int limit, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, Calendar>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(userId))
        {
            var calendars = await Session.Query<Calendar, CalendarShareIndex>(
                    index => index.PrincipalKind == CalendarPrincipalKind.User && index.PrincipalId == userId,
                    collection: CollectionName)
                .Take(limit)
                .ListAsync(cancellationToken);

            foreach (var calendar in calendars)
            {
                results.TryAdd(calendar.ItemId, calendar);
            }
        }

        var roleNames = roles?.Where(role => !string.IsNullOrEmpty(role)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];

        if (roleNames.Length > 0)
        {
            var calendars = await Session.Query<Calendar, CalendarShareIndex>(
                    index => index.PrincipalKind == CalendarPrincipalKind.Role && index.PrincipalId.IsIn(roleNames),
                    collection: CollectionName)
                .Take(limit)
                .ListAsync(cancellationToken);

            foreach (var calendar in calendars)
            {
                results.TryAdd(calendar.ItemId, calendar);
            }
        }

        return await LoadedAsync(results.Values);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Calendar>> ListVisibleToEveryoneAsync(int limit, CancellationToken cancellationToken = default)
    {
        var calendars = await Session.Query<Calendar, CalendarIndex>(index => index.Visibility != CalendarVisibility.Private, collection: CollectionName)
            .OrderBy(index => index.Name)
            .Take(limit)
            .ListAsync(cancellationToken);

        return await LoadedAsync(calendars);
    }

    private IQuery<Calendar, CalendarIndex> Filter(CalendarListQuery query)
    {
        var result = Session.Query<Calendar, CalendarIndex>(collection: CollectionName);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            result = result.Where(index => index.Name.Contains(search));
        }

        if (query.Types.Count > 0)
        {
            var types = query.Types.ToArray();

            result = result.Where(index => index.Type.IsIn(types));
        }

        if (!string.IsNullOrEmpty(query.OwnerId))
        {
            var ownerId = query.OwnerId;

            result = result.Where(index => index.OwnerId == ownerId);
        }

        if (query.OwnerIds.Count > 0)
        {
            var ownerIds = query.OwnerIds.ToArray();

            result = result.Where(index => index.OwnerId.IsIn(ownerIds));
        }

        if (!string.IsNullOrEmpty(query.Source))
        {
            var source = query.Source;

            result = result.Where(index => index.Source == source);
        }

        if (query.Enabled.HasValue)
        {
            var enabled = query.Enabled.Value;

            result = result.Where(index => index.Enabled == enabled);
        }

        return result;
    }
}
