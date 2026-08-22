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

    /// <inheritdoc/>
    public async Task<VoiceMediaItem> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return await Session.Query<VoiceMediaItem, VoiceMediaItemIndex>(
            index => index.Name == name,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<VoiceMediaItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await Session.Query<VoiceMediaItem, VoiceMediaItemIndex>(
            collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.Name)
            .ListAsync(cancellationToken);

        return items.ToArray();
    }
}
