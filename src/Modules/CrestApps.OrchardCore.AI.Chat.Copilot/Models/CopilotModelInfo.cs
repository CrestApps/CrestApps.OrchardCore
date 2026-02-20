namespace CrestApps.OrchardCore.AI.Chat.Copilot.Models;

/// <summary>
/// Represents a Copilot model available to the authenticated user.
/// </summary>
public sealed class CopilotModelInfo
{
    /// <summary>
    /// The model identifier (e.g., "gpt-4o", "claude-sonnet-4").
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// The display name of the model.
    /// </summary>
    public string Name { get; set; }
}
