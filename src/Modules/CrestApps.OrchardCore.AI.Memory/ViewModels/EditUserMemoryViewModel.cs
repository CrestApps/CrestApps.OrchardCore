namespace CrestApps.OrchardCore.AI.Memory.ViewModels;

/// <summary>
/// Represents the view model for edit user memory.
/// </summary>
public class EditUserMemoryViewModel
{
    /// <summary>
    /// Gets or sets the memory count.
    /// </summary>
    public int MemoryCount { get; set; }

    /// <summary>
    /// Gets or sets the current user identifier.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the local return URL back to the user profile editor.
    /// </summary>
    public string ReturnUrl { get; set; }
}
