using CrestApps.OrchardCore.DialPad.Models;

namespace CrestApps.OrchardCore.DialPad.ViewModels;

/// <summary>
/// View model for editing the DialPad provider settings. Production and sandbox credentials are edited
/// independently, and <see cref="Environment"/> selects which set the provider uses when connecting.
/// </summary>
public class DialPadSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the DialPad provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the active DialPad environment (production or sandbox) the provider connects to.
    /// </summary>
    public DialPadEnvironment Environment { get; set; }

    /// <summary>
    /// Gets or sets the credentials for the production DialPad environment.
    /// </summary>
    public DialPadEnvironmentSettingsViewModel Production { get; set; } = new();

    /// <summary>
    /// Gets or sets the credentials for the sandbox DialPad environment.
    /// </summary>
    public DialPadEnvironmentSettingsViewModel Sandbox { get; set; } = new();
}
