using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// A YesSql-based implementation of <see cref="IMessageTemplateStore"/>.
/// </summary>
public sealed class MessageTemplateStore : DocumentCatalog<MessageTemplate, MessageTemplateIndex>, IMessageTemplateStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageTemplateStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public MessageTemplateStore(ISession session)
        : base(session)
    {
        CollectionName = MessagingStorage.CollectionName;
    }
}
