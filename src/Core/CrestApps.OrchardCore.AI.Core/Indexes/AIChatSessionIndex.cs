using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

public sealed class AIChatSessionIndex : MapIndex
{
    public string SessionId { get; set; }

    public string ProfileId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Title { get; set; }

    public string UserId { get; set; }

    public string ClientId { get; set; }

    public int Status { get; set; }

    public DateTime LastActivityUtc { get; set; }
}
