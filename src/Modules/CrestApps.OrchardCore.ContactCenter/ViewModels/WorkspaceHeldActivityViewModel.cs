namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// An activity assigned to the agent that is still theirs to complete, with no call or offer showing it, on the agent's
/// bar and workspace.
/// </summary>
public sealed class WorkspaceHeldActivityViewModel
{
    /// <summary>
    /// Gets or sets the activity identifier.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the contact's name, or the number to reach them at when there is no contact name.
    /// </summary>
    public string CustomerLabel { get; set; }

    /// <summary>
    /// Gets or sets the name of the queue or campaign the work came from.
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Gets or sets the address of the activity's Complete screen.
    /// </summary>
    public string CompleteUrl { get; set; }

    /// <summary>
    /// Gets or sets when the work was assigned to the agent.
    /// </summary>
    public DateTime? AssignedUtc { get; set; }
}
