using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Hubs;

public class CompletionPartialMessage
{
    public string Content { get; set; }

    public Dictionary<string, AICompletionReference> References { get; set; }
}
