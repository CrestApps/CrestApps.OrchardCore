namespace CrestApps.OrchardCore.OpenAI.Models;

public class OpenAICompletedChatContext
{
    public OpenAIChatProfile Profile { get; set; }

    public int TotalHits { get; set; }

    public string Prompt { get; set; }

    public string SessionId { get; set; }

    public string UserId { get; set; }

    public string ClientId { get; set; }

    public IEnumerable<string> ContentItemIds { get; set; }

    public string MessageId { get; set; }
}
