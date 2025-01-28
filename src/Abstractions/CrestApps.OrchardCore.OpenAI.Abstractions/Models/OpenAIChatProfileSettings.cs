namespace CrestApps.OrchardCore.OpenAI.Models;

public class OpenAIChatProfileSettings
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

    /// <summary>
    /// Gets or sets a value indicating whether the profile is visible on the admin menu. This is only applicable to profiles with Chat type.
    /// </summary>
    public bool IsOnAdminMenu { get; set; }
}
