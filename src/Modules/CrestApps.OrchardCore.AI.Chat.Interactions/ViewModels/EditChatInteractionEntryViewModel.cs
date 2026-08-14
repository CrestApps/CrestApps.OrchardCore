namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// Represents the view model for edit chat interaction entry.
/// </summary>
public class EditChatInteractionEntryViewModel
{
    /// <summary>
    /// Gets or sets the item id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the editor.
    /// </summary>
    public dynamic Editor { get; set; }
}
