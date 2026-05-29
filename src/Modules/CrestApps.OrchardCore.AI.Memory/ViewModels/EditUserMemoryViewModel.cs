namespace CrestApps.OrchardCore.AI.Memory.ViewModels;

/// <summary>
/// Represents the view model for edit user memory.
/// </summary>
public class EditUserMemoryViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the user has any saved memories.
    /// </summary>
    public bool HasMemories { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current viewer is managing another user's memory.
    /// </summary>
    public bool IsOtherUser { get; set; }

    /// <summary>
    /// Gets or sets the current user identifier.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the local return URL back to the user profile editor.
    /// </summary>
    public string ReturnUrl { get; set; }
}
