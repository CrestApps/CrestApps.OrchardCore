using YesSql.Indexes;

namespace CrestApps.OrchardCore.OpenAI.Core.Indexes;

public class OpenAIChatSessionIndex : MapIndex
{
    public string SessionId { get; set; }

    public string ProfileId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Title { get; set; }

    public string UserId { get; set; }

    public string ClientId { get; set; }
}
