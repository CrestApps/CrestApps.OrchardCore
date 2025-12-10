using CrestApps.OrchardCore.YesSql.Core.Indexes;

namespace CrestApps.OrchardCore.AI.Chat.Indexes;

public sealed class AICustomChatInstanceIndex : CatalogItemIndex, ISourceAwareIndex, IDisplayTextAwareIndex
{
    public string Source { get; set; }

    public string DisplayText { get; set; }

    public string UserId { get; set; }

    public DateTime CreatedUtc { get; set; }
}
