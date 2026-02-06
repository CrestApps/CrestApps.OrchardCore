using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Indexes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Indexes;

internal sealed class ChatInteractionDocumentIndexProvider : IndexProvider<ChatInteractionDocument>
{
    public ChatInteractionDocumentIndexProvider()
    {
        CollectionName = AIConstants.CollectionName;
    }

    public override void Describe(DescribeContext<ChatInteractionDocument> context)
    {
        context
            .For<ChatInteractionDocumentIndex>()
            .Map(document =>
            {
                var extension = !string.IsNullOrEmpty(document.FileName)
                    ? Path.GetExtension(document.FileName)
                    : null;

                return new ChatInteractionDocumentIndex
                {
                    ItemId = document.ItemId,
                    ChatInteractionId = document.ChatInteractionId,
                    Extension = extension,
                };
            });
    }
}
