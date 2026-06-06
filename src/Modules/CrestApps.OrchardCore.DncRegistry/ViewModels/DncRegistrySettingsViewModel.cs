namespace CrestApps.OrchardCore.DncRegistry.ViewModels;

/// <summary>
/// View model for the DNC Registry global enforcement settings.
/// </summary>
public class DncRegistrySettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether DNC registry checking
    /// is globally enforced for all contact imports.
    /// </summary>
    public bool EnforceGlobally { get; set; }

    /// <summary>
    /// Gets or sets the registry keys that are enforced globally.
    /// </summary>
    public string[] EnforcedRegistryKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets the available registry entries for selection.
    /// </summary>
    public RegistryEntry[] AvailableRegistries { get; set; } = [];
}
