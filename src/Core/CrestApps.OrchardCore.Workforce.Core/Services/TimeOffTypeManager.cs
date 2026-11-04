using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ITimeOffTypeManager"/>.
/// </summary>
internal sealed class TimeOffTypeManager : CatalogManager<TimeOffType>, ITimeOffTypeManager
{
    private readonly TimeOffTypeStore _store;

    public TimeOffTypeManager(
        TimeOffTypeStore store,
        IEnumerable<ICatalogEntryHandler<TimeOffType>> handlers,
        ILogger<CatalogManager<TimeOffType>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<TimeOffType>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var types = (await _store.ListAsync(cancellationToken)).Where(type => type.Enabled).ToArray();

        foreach (var type in types)
        {
            await LoadAsync(type, cancellationToken);
        }

        return types;
    }
}
