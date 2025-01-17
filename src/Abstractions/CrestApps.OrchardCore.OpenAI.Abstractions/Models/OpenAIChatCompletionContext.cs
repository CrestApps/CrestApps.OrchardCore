namespace CrestApps.OrchardCore.OpenAI.Models;

public class OpenAIChatCompletionContext
{
    public string SystemMessage { get; set; }

    public OpenAIChatSession Session { get; set; }

    public OpenAIChatProfile Profile { get; }

    public bool UserMarkdownInResponse { get; set; }

    public bool DisableTools { get; set; }

    public OpenAIChatCompletionContext(OpenAIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Profile = profile;
    }
}
