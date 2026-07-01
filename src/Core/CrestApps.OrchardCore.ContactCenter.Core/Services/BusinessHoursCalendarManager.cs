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

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursCalendarManager"/> class.
    /// </summary>
    /// <param name="store">The underlying calendar store.</param>
    /// <param name="handlers">The catalog entry handlers for calendars.</param>
    /// <param name="logger">The logger instance.</param>
    public BusinessHoursCalendarManager(
        IBusinessHoursCalendarStore store,
        IEnumerable<ICatalogEntryHandler<BusinessHoursCalendar>> handlers,
        ILogger<CatalogManager<BusinessHoursCalendar>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
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
        var calendars = await _store.ListEnabledAsync(cancellationToken);

        foreach (var calendar in calendars)
        {
            await LoadAsync(calendar, cancellationToken);
        }

        return calendars;
    }
}
