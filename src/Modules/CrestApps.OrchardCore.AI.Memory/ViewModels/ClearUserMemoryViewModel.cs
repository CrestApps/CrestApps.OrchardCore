namespace CrestApps.OrchardCore.AI.Memory.ViewModels;

/// <summary>
/// Represents the confirmation page model for clearing the current user's AI memory.
/// </summary>
public sealed class ClearUserMemoryViewModel
{
    /// <summary>
    /// Gets or sets the current user identifier.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the number of saved memory entries for the current user.
    /// </summary>
    public int MemoryCount { get; set; }

    /// <summary>
    /// Gets or sets the local return URL back to the user profile editor.
    /// </summary>
    public string ReturnUrl { get; set; }
}
