namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Edits what the AI remembers about a contact.
/// </summary>
public class OmnichannelContactMemoryViewModel
{
    /// <summary>
    /// Gets or sets the memory note. Empty clears it.
    /// </summary>
    public string AIMemory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the note is rebuilt from the contact's past AI conversations on save.
    /// </summary>
    public bool Rebuild { get; set; }
}
