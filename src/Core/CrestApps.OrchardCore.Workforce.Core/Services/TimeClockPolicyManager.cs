using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ITimeClockPolicyManager"/>.
/// </summary>
internal sealed class TimeClockPolicyManager : CatalogManager<TimeClockPolicy>, ITimeClockPolicyManager
{
    private readonly TimeClockPolicyStore _store;

    public TimeClockPolicyManager(
        TimeClockPolicyStore store,
        IEnumerable<ICatalogEntryHandler<TimeClockPolicy>> handlers,
        ILogger<CatalogManager<TimeClockPolicy>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<TimeClockPolicy>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var items = (await _store.ListAsync(cancellationToken)).Where(item => item.Enabled).ToArray();

        foreach (var item in items)
        {
            await LoadAsync(item, cancellationToken);
        }

        return items;
    }
}
