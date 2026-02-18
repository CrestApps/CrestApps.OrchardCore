using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Indexes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;

public sealed class DefaultChatInteractionDocumentStore : DocumentCatalog<ChatInteractionDocument, ChatInteractionDocumentIndex>, IChatInteractionDocumentStore
{
    public DefaultChatInteractionDocumentStore(ISession session)
        : base(session)
    {
        CollectionName = AIConstants.CollectionName;
    }

    public async Task<IReadOnlyCollection<ChatInteractionDocument>> GetDocuments(string chatInteractionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(chatInteractionId);

        return (await Session.Query<ChatInteractionDocument, ChatInteractionDocumentIndex>(x => x.ChatInteractionId == chatInteractionId, CollectionName).ListAsync()).ToArray();
    }
}
