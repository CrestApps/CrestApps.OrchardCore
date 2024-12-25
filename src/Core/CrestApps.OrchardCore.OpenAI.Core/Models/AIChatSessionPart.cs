using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.OpenAI.Core.Models;

public sealed class AIChatSessionPart : ContentPart
{
    public IList<AIChatSessionMessage> Prompts { get; set; } = [];
}
