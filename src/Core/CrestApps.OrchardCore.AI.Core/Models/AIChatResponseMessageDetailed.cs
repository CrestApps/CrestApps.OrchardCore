using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class AIChatResponseMessageDetailed : AIResponseMessage
{
    public string Id { get; set; }

    public string Role { get; set; }

    public bool IsGeneratedPrompt { get; set; }

    public string Title { get; set; }

    public Dictionary<string, AICompletionReference> References { get; set; }
}
