using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Keeps calendar categories in the Calendars collection.
/// </summary>
public sealed class DefaultCalendarCategoryService : ICalendarCategoryService
{
    private const int MaxCategories = 500;

    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCalendarCategoryService"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="clock">The clock.</param>
    public DefaultCalendarCategoryService(ISession session, IClock clock)
    {
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarCategory>> ListAsync(string userId, CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrEmpty(userId)
            ? _session.Query<CalendarCategory, CalendarCategoryIndex>(index => index.OwnerId == null, collection: CalendarsConstants.CollectionName)
            : _session.Query<CalendarCategory, CalendarCategoryIndex>(index => index.OwnerId == null || index.OwnerId == userId, collection: CalendarsConstants.CollectionName);

        return [.. (await query.Take(MaxCategories).ListAsync(cancellationToken)).OrderBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarCategory>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var keys = ids?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        if (keys.Length == 0)
        {
            return [];
        }

        return [.. await _session.Query<CalendarCategory, CalendarCategoryIndex>(index => index.ItemId.IsIn(keys), collection: CalendarsConstants.CollectionName).ListAsync(cancellationToken)];
    }

    /// <inheritdoc />
    public Task<CalendarCategory> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Task.FromResult<CalendarCategory>(null);
        }

        return _session.Query<CalendarCategory, CalendarCategoryIndex>(index => index.ItemId == id, collection: CalendarsConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(CalendarCategory category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (string.IsNullOrEmpty(category.ItemId))
        {
            category.ItemId = CrestApps.Core.UniqueId.GenerateId();
            category.CreatedUtc = _clock.UtcNow;
        }

        return _session.SaveAsync(category, false, CalendarsConstants.CollectionName, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(CalendarCategory category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        _session.Delete(category, CalendarsConstants.CollectionName);

        return Task.CompletedTask;
    }
}
