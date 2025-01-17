using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class OpenAIChatSessionPrompt
{
    public string Id { get; set; }

    public ChatRole Role { get; set; }

    public string Content { get; set; }

    public string Title { get; set; }

    public bool IsGeneratedPrompt { get; set; }
}
