namespace CrestApps.OrchardCore.AI.Models;

public class AIProfileSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the system message is locked to prevent the user from changing it.
    /// </summary>
    public bool LockSystemMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the profile is listable on the UI.
    /// </summary>
    public bool IsListable { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the profile is removable.
    /// </summary>
    public bool IsRemovable { get; set; } = true;
}
