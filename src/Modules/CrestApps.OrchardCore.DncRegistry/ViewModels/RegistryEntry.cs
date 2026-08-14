namespace CrestApps.OrchardCore.DncRegistry.ViewModels;

/// <summary>
/// Represents a registry entry displayed in the settings UI.
/// </summary>
public sealed class RegistryEntry
{
    /// <summary>
    /// Gets or sets the unique key of the registry.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the localized display name of the registry.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the localized description of the registry.
    /// </summary>
    public string Description { get; set; }
}
