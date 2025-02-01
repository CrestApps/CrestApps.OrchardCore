namespace CrestApps.OrchardCore.AI.Models;

public class AIChatCompletionContext
{
    public AIChatSession Session { get; set; }

    public AIChatProfile Profile { get; }

    /// <summary>
    /// If the profile contains no valid deployment Id, we can use this property to set a fallback deployment Id.
    /// </summary>
    public string DeploymentId { get; set; }

    public bool UserMarkdownInResponse { get; set; }

    public bool DisableTools { get; set; }

    public AIChatCompletionContext(AIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Profile = profile;
    }
}
