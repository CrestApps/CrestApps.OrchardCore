namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class ChatCompletionResponse
{
    public IEnumerable<ChatCompletionChoice> Choices { get; set; }

    public static readonly ChatCompletionResponse Empty = new()
    {
        Choices = []
    };
}
