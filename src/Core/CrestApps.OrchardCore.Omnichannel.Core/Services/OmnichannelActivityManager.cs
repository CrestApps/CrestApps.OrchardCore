using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IOmnichannelActivityManager"/> that delegates storage
/// to <see cref="IOmnichannelActivityStore"/> and loads entries through catalog handlers.
/// </summary>
public sealed class OmnichannelActivityManager : CatalogManager<OmnichannelActivity>, IOmnichannelActivityManager
{
    private readonly IOmnichannelActivityStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityManager"/> class.
    /// </summary>
    /// <param name="omnichannelActivityStore">The underlying activity store.</param>
    /// <param name="handlers">The catalog entry handlers for activity entries.</param>
    /// <param name="logger">The logger instance.</param>
    public OmnichannelActivityManager(
        IOmnichannelActivityStore omnichannelActivityStore,
        IEnumerable<ICatalogEntryHandler<OmnichannelActivity>> handlers,
        ILogger<CatalogManager<OmnichannelActivity>> logger)
    : base(omnichannelActivityStore, handlers, logger)
    {
        _store = omnichannelActivityStore;
    }

    /// <inheritdoc/>
    public async Task<PageResult<OmnichannelActivity>> PageContactManualScheduledAsync(string contentContentItemId, int page, int pageSize)
    {
        var result = await _store.PageContactManualScheduledAsync(contentContentItemId, page, pageSize);

        foreach (var entry in result.Entries)
        {
            await LoadAsync(entry);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<PageResult<OmnichannelActivity>> PageManualScheduledAsync(string userId, int page, int pageSize, ListOmnichannelActivityFilter filter)
    {
        var result = await _store.PageManualScheduledAsync(userId, page, pageSize, filter);

        foreach (var entry in result.Entries)
        {
            await LoadAsync(entry);
        }

        return result;
    }

    /// <inheritdoc/>
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
