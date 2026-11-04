using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ITimeClockKioskManager"/>.
/// </summary>
internal sealed class TimeClockKioskManager : CatalogManager<TimeClockKiosk>, ITimeClockKioskManager
{
    private readonly TimeClockKioskStore _store;

    public TimeClockKioskManager(
        TimeClockKioskStore store,
        IEnumerable<ICatalogEntryHandler<TimeClockKiosk>> handlers,
        ILogger<CatalogManager<TimeClockKiosk>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<TimeClockKiosk>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var items = (await _store.ListAsync(cancellationToken)).Where(item => item.Enabled).ToArray();

        foreach (var item in items)
        {
            await LoadAsync(item, cancellationToken);
        }

        return items;
    }
}
