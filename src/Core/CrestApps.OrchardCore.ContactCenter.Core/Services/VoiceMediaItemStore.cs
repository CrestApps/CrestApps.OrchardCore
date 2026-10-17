using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IVoiceMediaItemStore"/>.
/// </summary>
public sealed class VoiceMediaItemStore : DocumentCatalog<VoiceMediaItem, VoiceMediaItemIndex>, IVoiceMediaItemStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceMediaItemStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public VoiceMediaItemStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <summary>
    /// Folds the single provider copy an entry recorded before copies were tracked per provider into
    /// <see cref="VoiceMediaItem.ProviderReferences"/>, so every reader sees one record of the copies that exist.
    /// </summary>
    /// <param name="record">The entry read from storage.</param>
    /// <returns>A completed task.</returns>
    protected override ValueTask LoadingAsync(VoiceMediaItem record)
    {
        VoiceMediaItemCompatibility.FoldLegacyReference(record);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<VoiceMediaItem> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var item = await Session.Query<VoiceMediaItem, VoiceMediaItemIndex>(
            index => index.Name == name,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        return await LoadedAsync(item);
    }

    /// <inheritdoc/>
    public new async Task<IReadOnlyCollection<VoiceMediaItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await Session.Query<VoiceMediaItem, VoiceMediaItemIndex>(
            collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.Name)
            .ListAsync(cancellationToken);

        return await LoadedAsync(items);
    }
}
