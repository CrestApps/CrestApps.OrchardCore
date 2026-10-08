using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default implementation of <see cref="IMessageTemplateManager"/>.
/// </summary>
public sealed class MessageTemplateManager : CatalogManager<MessageTemplate>, IMessageTemplateManager
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageTemplateManager"/> class.
    /// </summary>
    /// <param name="store">The underlying template store.</param>
    /// <param name="handlers">The catalog entry handlers for templates.</param>
    /// <param name="logger">The logger instance.</param>
    public MessageTemplateManager(
        IMessageTemplateStore store,
        IEnumerable<ICatalogEntryHandler<MessageTemplate>> handlers,
        ILogger<CatalogManager<MessageTemplate>> logger)
        : base(store, handlers, logger)
    {
    }
}
