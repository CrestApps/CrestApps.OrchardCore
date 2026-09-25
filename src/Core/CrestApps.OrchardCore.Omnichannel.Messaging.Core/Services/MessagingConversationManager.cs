using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default implementation of <see cref="IMessagingConversationManager"/>.
/// </summary>
public sealed class MessagingConversationManager : CatalogManager<MessagingConversation>, IMessagingConversationManager
{
    private readonly IMessagingConversationStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingConversationManager"/> class.
    /// </summary>
    /// <param name="store">The underlying conversation store.</param>
    /// <param name="handlers">The catalog entry handlers for conversations.</param>
    /// <param name="logger">The logger instance.</param>
    public MessagingConversationManager(
        IMessagingConversationStore store,
        IEnumerable<ICatalogEntryHandler<MessagingConversation>> handlers,
        ILogger<CatalogManager<MessagingConversation>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<MessagingConversation> FindByAddressesAsync(string channel, string serviceAddress, string contactAddress, CancellationToken cancellationToken = default)
    {
        var conversation = await _store.FindByAddressesAsync(channel, serviceAddress, contactAddress, cancellationToken);

        if (conversation is not null)
        {
            await LoadAsync(conversation, cancellationToken);
        }

        return conversation;
    }
}
