using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default implementation of <see cref="IMessagingBroadcastManager"/>.
/// </summary>
public sealed class MessagingBroadcastManager : CatalogManager<MessagingBroadcast>, IMessagingBroadcastManager
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingBroadcastManager"/> class.
    /// </summary>
    /// <param name="store">The underlying broadcast store.</param>
    /// <param name="handlers">The catalog entry handlers for broadcasts.</param>
    /// <param name="logger">The logger instance.</param>
    public MessagingBroadcastManager(
        IMessagingBroadcastStore store,
        IEnumerable<ICatalogEntryHandler<MessagingBroadcast>> handlers,
        ILogger<CatalogManager<MessagingBroadcast>> logger)
        : base(store, handlers, logger)
    {
    }
}
