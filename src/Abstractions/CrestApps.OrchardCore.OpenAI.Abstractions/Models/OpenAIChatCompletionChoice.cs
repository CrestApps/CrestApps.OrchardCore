namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class OpenAIChatCompletionChoice
{
    public string Message { get; set; }

    public IList<string> ContentItemIds { get; set; }
}
