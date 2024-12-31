namespace CrestApps.OrchardCore.OpenAI.Models;

public class ChatCompletionContext
{
    public AIChatSession Session { get; set; }

    public AIChatProfile Profile { get; }

    public bool UserMarkdownInResponse { get; set; }

    public ChatCompletionContext(AIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Profile = profile;
    }
}
