namespace CrestApps.OrchardCore.AI.Endpoints.Models;

internal sealed class AIUtilityCompletionRequest
{
    /// <summary>
    /// Gets or sets the profile id.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the prompt.
    /// </summary>
    public string Prompt { get; set; }
}
