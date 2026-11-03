namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Edits whether an AI profile reads and writes contact memory.
/// </summary>
public class EditAIProfileContactMemoryViewModel
{
    /// <summary>
    /// Gets or sets whether the profile's instructions get the contact's memory note.
    /// </summary>
    public bool UseMemory { get; set; }

    /// <summary>
    /// Gets or sets whether the profile's conversations rewrite the contact's memory note when they end.
    /// </summary>
    public bool SaveMemory { get; set; }
}
