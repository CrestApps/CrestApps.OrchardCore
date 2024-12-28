namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class ChatCompletionChoice
{
    public string Message { get; set; }

    public IList<string> ContentItemIds { get; set; }
}
