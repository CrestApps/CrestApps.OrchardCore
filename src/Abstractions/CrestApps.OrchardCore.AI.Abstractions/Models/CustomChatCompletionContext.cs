namespace CrestApps.OrchardCore.AI.Models;

public sealed class CustomChatCompletionContext
{
    public string CustomChatInstanceId { get; set; }

    public CustomChatSession Session { get; set; }
}
