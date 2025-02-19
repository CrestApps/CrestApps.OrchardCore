namespace CrestApps.OrchardCore.AI.Models;

public class AICompletionContext
{
    public AIChatSession Session { get; set; }

    public AIProfile Profile { get; set; }

    public string SystemMessage { get; set; }

    public bool UserMarkdownInResponse { get; set; } = true;

    public bool DisableTools { get; set; }

    public bool UseCaching { get; set; } = true;
}
