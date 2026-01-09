using CrestApps.OrchardCore.YesSql.Core.Indexes;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

public sealed class ChatInteractionIndex : CatalogItemIndex, ISourceAwareIndex
{
    public string UserId { get; set; }

    public string Source { get; set; }

    public string Title { get; set; }

    public DateTime CreatedUtc { get; set; }
}
