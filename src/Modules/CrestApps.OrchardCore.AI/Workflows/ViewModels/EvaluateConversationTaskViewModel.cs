namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Edits the Evaluate AI Conversation task.
/// </summary>
public class EvaluateConversationTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression for the AI session to evaluate.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the criteria, one per line.
    /// </summary>
    public string Criteria { get; set; }

    /// <summary>
    /// Gets or sets the lowest score that passes.
    /// </summary>
    public int PassingScore { get; set; }

    /// <summary>
    /// Gets or sets the label the evaluation is kept under.
    /// </summary>
    public string Label { get; set; }
}
