namespace CrestApps.OrchardCore.AI.Models;

public sealed class AIChatCompletionResponse
{
    public IEnumerable<AIChatCompletionChoice> Choices { get; set; }

    public static readonly AIChatCompletionResponse Empty = new()
    {
        Choices = []
    };
}
