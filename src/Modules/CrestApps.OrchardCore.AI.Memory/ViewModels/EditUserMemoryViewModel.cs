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
    /// Gets or sets the clear memories.
    /// </summary>
    public bool ClearMemories { get; set; }

    /// <summary>
    /// Gets or sets the confirm clear memories.
    /// </summary>
    public bool ConfirmClearMemories { get; set; }
}
