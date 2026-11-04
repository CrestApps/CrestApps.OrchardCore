using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="IPayCodeManager"/>.
/// </summary>
internal sealed class PayCodeManager : CatalogManager<PayCode>, IPayCodeManager
{
    private readonly PayCodeStore _store;

    public PayCodeManager(
        PayCodeStore store,
        IEnumerable<ICatalogEntryHandler<PayCode>> handlers,
        ILogger<CatalogManager<PayCode>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<PayCode>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var items = (await _store.ListAsync(cancellationToken)).Where(item => item.Enabled).ToArray();

        foreach (var item in items)
        {
            await LoadAsync(item, cancellationToken);
        }

        return items;
    }
}
