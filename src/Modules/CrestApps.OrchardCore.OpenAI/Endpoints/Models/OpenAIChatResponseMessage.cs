namespace CrestApps.OrchardCore.OpenAI.Endpoints.Models;

internal sealed class OpenAIChatResponseMessage
{
    public string Id { get; set; }

    public string Role { get; set; }

    public bool IsGeneratedPrompt { get; set; }

    public string Title { get; set; }

    public string Content { get; set; }

    public string ContentHTML { get; set; }
}
