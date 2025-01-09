namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class OpenAIChatCompletionResponse
{
    public IEnumerable<OpenAIChatCompletionChoice> Choices { get; set; }

    public static readonly OpenAIChatCompletionResponse Empty = new()
    {
        Choices = []
    };
}
