namespace CrestApps.OrchardCore.DncRegistry.Models;

/// <summary>
/// Global site settings for the DNC Registry module.
/// Controls global enforcement behavior across all imports.
/// </summary>
public sealed class DncRegistrySettings
{
    /// <summary>
    /// Gets or sets a value indicating whether DNC registry checking
    /// is globally enforced for all contact imports.
    /// </summary>
    public bool EnforceGlobally { get; set; }

    /// <summary>
    /// Gets or sets the registry keys that are enforced globally.
    /// When <see cref="EnforceGlobally"/> is <see langword="true"/>,
    /// these registries are always checked during import.
    /// </summary>
    public string[] EnforcedRegistryKeys { get; set; } = [];
}
