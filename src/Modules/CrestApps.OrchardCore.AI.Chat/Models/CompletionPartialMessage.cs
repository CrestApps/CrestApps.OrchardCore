using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Models;

public class CompletionPartialMessage
{
    public string MessageId { get; set; }

    public string Content { get; set; }

    public string SessionId { get; set; }

    public Dictionary<string, AICompletionReference> References { get; set; }
}
