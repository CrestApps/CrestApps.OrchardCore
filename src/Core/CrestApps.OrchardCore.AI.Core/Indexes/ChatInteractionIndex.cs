using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

public sealed class ChatInteractionIndex : MapIndex
{
    public string InteractionId { get; set; }

    public string UserId { get; set; }

    public string Title { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }
}
