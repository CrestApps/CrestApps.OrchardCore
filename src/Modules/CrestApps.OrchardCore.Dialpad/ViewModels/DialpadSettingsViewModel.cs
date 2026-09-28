using CrestApps.OrchardCore.Dialpad.Models;

namespace CrestApps.OrchardCore.Dialpad.ViewModels;

/// <summary>
/// View model for editing the Dialpad provider settings. Production and sandbox credentials are edited
/// independently, and <see cref="Environment"/> selects which set the provider uses when connecting.
/// </summary>
public class DialpadSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the Dialpad provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the active Dialpad environment (production or sandbox) the provider connects to.
    /// </summary>
    public DialpadEnvironment Environment { get; set; }

    /// <summary>
    /// Gets or sets the credentials for the production Dialpad environment.
    /// </summary>
    public DialpadEnvironmentSettingsViewModel Production { get; set; } = new();

    /// <summary>
    /// Gets or sets the credentials for the sandbox Dialpad environment.
    /// </summary>
    public DialpadEnvironmentSettingsViewModel Sandbox { get; set; } = new();
}
