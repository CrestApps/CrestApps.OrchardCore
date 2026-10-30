namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Edits the Set Conversation Variables task.
/// </summary>
public class SetConversationVariablesTaskViewModel
{
    /// <summary>
    /// Gets or sets the variables, one <c>name: value</c> per line.
    /// </summary>
    public string Variables { get; set; }
}
