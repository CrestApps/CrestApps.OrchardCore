using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.OpenAI.Core.Models;

public sealed class OpenAIChatSessionPart : ContentPart
{
    public IList<OpenAIChatSessionMessage> Prompts { get; set; } = [];
}
