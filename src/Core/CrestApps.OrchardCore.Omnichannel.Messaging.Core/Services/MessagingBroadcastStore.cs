using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// A YesSql-based implementation of <see cref="IMessagingBroadcastStore"/>.
/// </summary>
public sealed class MessagingBroadcastStore : DocumentCatalog<MessagingBroadcast, MessagingBroadcastIndex>, IMessagingBroadcastStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingBroadcastStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public MessagingBroadcastStore(ISession session)
        : base(session)
    {
        CollectionName = MessagingStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<MessagingBroadcast>> GetByStatusAsync(MessagingBroadcastStatus status, CancellationToken cancellationToken = default)
    {
        var value = status.ToString();

        var broadcasts = await Session.Query<MessagingBroadcast, MessagingBroadcastIndex>(
                index => index.Status == value,
                collection: MessagingStorage.CollectionName)
            .ListAsync(cancellationToken);

        return broadcasts.ToArray();
    }
}
