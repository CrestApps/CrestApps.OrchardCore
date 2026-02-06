using CrestApps.OrchardCore.YesSql.Core.Indexes;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Indexes;

public sealed class ChatInteractionDocumentIndex : CatalogItemIndex
{
    public string ChatInteractionId { get; set; }

    public string Extension { get; set; }
}
