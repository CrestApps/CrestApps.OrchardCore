namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Edits the Update Contact Memory task.
/// </summary>
public class UpdateContactMemoryTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression for the AI session to remember.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression for the contact.
    /// </summary>
    public string ContactId { get; set; }
}
