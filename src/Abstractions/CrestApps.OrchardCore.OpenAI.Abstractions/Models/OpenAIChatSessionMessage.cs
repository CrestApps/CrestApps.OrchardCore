namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class OpenAIChatSessionMessage
{
    public string Id { get; set; }

    public string Role { get; set; }

    public string Prompt { get; set; }

    public string Title { get; set; }

    public bool IsGeneratedPrompt { get; set; }
}
