using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="IActivityCodeManager"/>.
/// </summary>
internal sealed class ActivityCodeManager : CatalogManager<ActivityCode>, IActivityCodeManager
{
    private readonly ActivityCodeStore _store;

    public ActivityCodeManager(
        ActivityCodeStore store,
        IEnumerable<ICatalogEntryHandler<ActivityCode>> handlers,
        ILogger<CatalogManager<ActivityCode>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<ActivityCode>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        var codes = await _store.GetEnabledAsync(cancellationToken);

        foreach (var code in codes)
        {
            await LoadAsync(code, cancellationToken);
        }

        return codes;
    }
}
