namespace CrestApps.OrchardCore.AI.Endpoints.Models;

internal sealed class AICompletionRequest
{
    /// <summary>
    /// Gets or sets the session id.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the profile id.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the prompt.
    /// </summary>
    public string Prompt { get; set; }

    /// <summary>
    /// Gets or sets the session profile id.
    /// </summary>
    public string SessionProfileId { get; set; }
}
