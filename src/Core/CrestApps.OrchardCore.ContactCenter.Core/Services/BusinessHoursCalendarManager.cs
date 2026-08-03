using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IBusinessHoursCalendarManager"/>.
/// </summary>
public sealed class BusinessHoursCalendarManager : CatalogManager<BusinessHoursCalendar>, IBusinessHoursCalendarManager
{
    private readonly IBusinessHoursCalendarStore _store;
    private readonly IContactCenterConfigurationCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursCalendarManager"/> class.
    /// </summary>
    /// <param name="store">The underlying calendar store.</param>
    /// <param name="handlers">The catalog entry handlers for calendars.</param>
    /// <param name="cache">The routing configuration cache used to serve enabled calendars without re-querying the store.</param>
    /// <param name="logger">The logger instance.</param>
    public BusinessHoursCalendarManager(
        IBusinessHoursCalendarStore store,
        IEnumerable<ICatalogEntryHandler<BusinessHoursCalendar>> handlers,
        IContactCenterConfigurationCache cache,
        ILogger<CatalogManager<BusinessHoursCalendar>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
        _cache = cache;
    }

    /// <inheritdoc/>
    public async Task<BusinessHoursCalendar> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var calendar = await _store.FindByNameAsync(name, cancellationToken);

        if (calendar is not null)
        {
            await LoadAsync(calendar, cancellationToken);
        }

        return calendar;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<BusinessHoursCalendar>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetEnabledAsync(async token =>
        {
            var calendars = await _store.ListEnabledAsync(token);

            foreach (var calendar in calendars)
            {
                await LoadAsync(calendar, token);
            }

            return calendars;
        }, cancellationToken);
    }
}
