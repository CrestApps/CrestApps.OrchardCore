namespace CrestApps.OrchardCore.AI.Chat.Copilot.Models;

/// <summary>
/// Settings specific to Copilot orchestrator configuration.
/// </summary>
public sealed class CopilotProfileSettings
{
    /// <summary>
    /// The Copilot model to use (e.g., gpt-4o, claude-3.5-sonnet).
    /// </summary>
    public string CopilotModel { get; set; }

    /// <summary>
    /// Additional Copilot execution flags (e.g., --allow-all).
    /// </summary>
    public string CopilotFlags { get; set; }
}
