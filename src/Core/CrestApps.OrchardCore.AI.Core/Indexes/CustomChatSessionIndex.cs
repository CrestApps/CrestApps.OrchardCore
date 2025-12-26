using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

public sealed class CustomChatSessionIndex : MapIndex
{
    public string SessionId { get; set; }


    public string CustomChatInstanceId { get; set; }


    public string UserId { get; set; }


    public string Source { get; set; }


    public string DisplayText { get; set; }


    public DateTime CreatedUtc { get; set; }
}
