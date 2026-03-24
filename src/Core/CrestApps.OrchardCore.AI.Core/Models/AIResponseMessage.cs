using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Models;

public class AIResponseMessage
{
    public string Content { get; set; }

    public AssistantMessageAppearance Appearance { get; set; }
}
