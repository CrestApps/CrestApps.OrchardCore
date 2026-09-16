namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the live depth of a queue the agent is signed in to, shown on the agent desktop.
/// </summary>
public sealed class WorkspaceQueueStatViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the queue.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the queue.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the number of items currently waiting in the queue.
    /// </summary>
    public int WaitingCount { get; set; }
}
