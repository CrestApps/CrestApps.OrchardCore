using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IVoiceMediaItemManager"/>.
/// </summary>
public sealed class VoiceMediaItemManager : CatalogManager<VoiceMediaItem>, IVoiceMediaItemManager
{
    private readonly IVoiceMediaItemStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceMediaItemManager"/> class.
    /// </summary>
    /// <param name="store">The underlying media library store.</param>
    /// <param name="handlers">The catalog entry handlers for media clips.</param>
    /// <param name="logger">The logger instance.</param>
    public VoiceMediaItemManager(
        IVoiceMediaItemStore store,
        IEnumerable<ICatalogEntryHandler<VoiceMediaItem>> handlers,
        ILogger<CatalogManager<VoiceMediaItem>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<VoiceMediaItem> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var item = await _store.FindByNameAsync(name, cancellationToken);

        if (item is not null)
        {
            await LoadAsync(item, cancellationToken);
        }

        return item;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<VoiceMediaItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _store.GetAllAsync(cancellationToken);

        foreach (var item in items)
        {
            await LoadAsync(item, cancellationToken);
        }

        return items;
    }
}
