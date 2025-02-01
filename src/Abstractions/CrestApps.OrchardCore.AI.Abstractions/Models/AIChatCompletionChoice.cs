namespace CrestApps.OrchardCore.AI.Models;

public sealed class AIChatCompletionChoice
{
    public string Content { get; set; }

    public IList<string> ContentItemIds { get; set; }
}
