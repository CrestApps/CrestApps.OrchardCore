namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Represents the primary view model for the AI completion with config task activity.
/// </summary>
public class AICompletionWithConfigTaskViewModel
{
    /// <summary>
    /// Gets or sets the prompt template.
    /// </summary>
    public string PromptTemplate { get; set; }

    /// <summary>
    /// Gets or sets the result property name.
    /// </summary>
    public string ResultPropertyName { get; set; }
}
