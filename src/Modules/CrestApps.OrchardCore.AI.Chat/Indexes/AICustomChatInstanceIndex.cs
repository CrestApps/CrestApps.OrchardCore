using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Chat.Indexes;

public sealed class AICustomChatInstanceIndex : MapIndex
{
    public string InstanceId { get; set; }

    public string Title { get; set; }

    public string UserId { get; set; }

    public DateTime CreatedUtc { get; set; }
}
