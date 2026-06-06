namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// View model for the national do-not-call registry import options UI.
/// </summary>
public class NationalDoNotCallRegistryImportOptionsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether to ignore phone numbers on the national do-not-call list.
    /// </summary>
    public bool IgnoreDoNotCallNumbers { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether DNC checking is globally enforced by site settings.
    /// </summary>
    public bool IsGloballyEnforced { get; set; }

    /// <summary>
    /// Gets or sets the registry keys selected for DNC checking.
    /// </summary>
    public string[] SelectedRegistryKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets the available registry options.
    /// </summary>
    public NationalDoNotCallRegistryEntry[] AvailableRegistries { get; set; } = [];
}

/// <summary>
/// Represents an available registry entry for display in the UI.
/// </summary>
public sealed class NationalDoNotCallRegistryEntry
{
    /// <summary>
    /// Gets or sets the unique key of the registry.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the display name of the registry.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description of the registry.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this registry is globally enforced.
    /// </summary>
    public bool IsEnforced { get; set; }
}
