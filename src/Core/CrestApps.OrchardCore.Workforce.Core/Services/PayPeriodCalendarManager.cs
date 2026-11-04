using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="IPayPeriodCalendarManager"/>.
/// </summary>
internal sealed class PayPeriodCalendarManager : CatalogManager<PayPeriodCalendar>, IPayPeriodCalendarManager
{
    private readonly PayPeriodCalendarStore _store;

    public PayPeriodCalendarManager(
        PayPeriodCalendarStore store,
        IEnumerable<ICatalogEntryHandler<PayPeriodCalendar>> handlers,
        ILogger<CatalogManager<PayPeriodCalendar>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<PayPeriodCalendar>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var items = (await _store.ListAsync(cancellationToken)).Where(item => item.Enabled).ToArray();

        foreach (var item in items)
        {
            await LoadAsync(item, cancellationToken);
        }

        return items;
    }
}
