using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

public sealed class OmnichannelActivityManager : CatalogManager<OmnichannelActivity>, IOmnichannelActivityManager
{
    private readonly IOmnichannelActivityStore _store;

    public OmnichannelActivityManager(
        IOmnichannelActivityStore omnichannelActivityStore,
        IEnumerable<ICatalogEntryHandler<OmnichannelActivity>> handlers,
        ILogger<CatalogManager<OmnichannelActivity>> logger)
        : base(omnichannelActivityStore, handlers, logger)
    {
        _store = omnichannelActivityStore;
    }

    public async Task<PageResult<OmnichannelActivity>> PageContactManualScheduledAsync(string contentContentItemId, int page, int pageSize)
    {
        var result = await _store.PageContactManualScheduledAsync(contentContentItemId, page, pageSize);

        foreach (var entry in result.Entries)
        {
            await LoadAsync(entry);
        }

        return result;
    }

    public async Task<PageResult<OmnichannelActivity>> PageManualScheduledAsync(string userId, int page, int pageSize, ListOmnichannelActivityFilter filter)
    {
        var result = await _store.PageManualScheduledAsync(userId, page, pageSize, filter);

        foreach (var entry in result.Entries)
        {
            await LoadAsync(entry);
        }

        return result;
    }

    public async Task<PageResult<OmnichannelActivity>> PageContactManualCompletedAsync(string contentContentItemId, int page, int pageSize)
    {
        var result = await _store.PageContactManualCompletedAsync(contentContentItemId, page, pageSize);

        foreach (var entry in result.Entries)
        {
            await LoadAsync(entry);
        }

        return result;
    }
}
