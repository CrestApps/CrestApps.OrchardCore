namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Edits the Rerun AI Conversation Analysis task.
/// </summary>
public class RerunConversationAnalysisTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression for the AI session to analyze again.
    /// </summary>
    public string SessionId { get; set; }
}
