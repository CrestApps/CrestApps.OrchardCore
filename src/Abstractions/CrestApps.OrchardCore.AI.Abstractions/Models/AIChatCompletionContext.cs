namespace CrestApps.OrchardCore.AI.Models;

public class AIChatCompletionContext
{
    public AIChatSession Session { get; set; }

    public AIProfile Profile { get; set; }

    public string SystemMessage { get; set; }

    public bool UserMarkdownInResponse { get; set; }

    public bool DisableTools { get; set; }
}
