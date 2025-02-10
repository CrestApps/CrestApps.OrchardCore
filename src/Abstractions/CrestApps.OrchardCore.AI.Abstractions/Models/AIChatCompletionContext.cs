namespace CrestApps.OrchardCore.AI.Models;

public class AIChatCompletionContext
{
    public AIChatSession Session { get; set; }

    public AIProfile Profile { get; }

    public bool UserMarkdownInResponse { get; set; }

    public bool DisableTools { get; set; }

    public AIChatCompletionContext(AIProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Profile = profile;
    }
}
