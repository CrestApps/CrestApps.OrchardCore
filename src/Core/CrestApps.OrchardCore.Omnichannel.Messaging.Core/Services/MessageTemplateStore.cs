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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MessageTemplate>> ListByScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrEmpty(scope)
            ? Session.Query<MessageTemplate, MessageTemplateIndex>(index => index.Scope == null || index.Scope == string.Empty, collection: CollectionName)
            : Session.Query<MessageTemplate, MessageTemplateIndex>(index => index.Scope == scope, collection: CollectionName);

        return (await query.OrderBy(index => index.Name).ListAsync(cancellationToken)).ToArray();
    }
}
