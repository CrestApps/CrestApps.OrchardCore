using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI.Endpoints.Models;

internal sealed class OpenAIChatResponseMessageDetailed : OpenAIChatResponseMessage
{
    public string Id { get; set; }

    public string Role { get; set; }

    public bool IsGeneratedPrompt { get; set; }

    public string Title { get; set; }
}
