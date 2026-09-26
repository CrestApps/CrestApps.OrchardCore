namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents a lightweight identifier and label pair rendered by the agent desktop, for example a
/// signed-in queue or a selectable disposition.
/// </summary>
public sealed class WorkspaceLookupViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the item.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the display label of the item.
    /// </summary>
    public string Name { get; set; }
}
