namespace CrestApps.OrchardCore.DialPad.Models;

/// <summary>
/// Represents the DialPad provider site settings. Production and sandbox credentials are configured
/// independently, and the active <see cref="Environment"/> selects which credential set the provider uses
/// when connecting.
/// </summary>
public sealed class DialPadSettings
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
    /// Gets or sets the credentials and options for the production DialPad environment.
    /// </summary>
    public DialPadEnvironmentSettings Production { get; set; } = new();

    /// <summary>
    /// Gets or sets the credentials and options for the sandbox DialPad environment.
    /// </summary>
    public DialPadEnvironmentSettings Sandbox { get; set; } = new();

    /// <summary>
    /// Gets the credentials for the specified DialPad environment, creating an empty set when none exists.
    /// </summary>
    /// <param name="environment">The environment whose credentials are requested.</param>
    /// <returns>The credentials for the environment.</returns>
    public DialPadEnvironmentSettings GetEnvironmentSettings(DialPadEnvironment environment)
    {
        if (environment == DialPadEnvironment.Sandbox)
        {
            return Sandbox ??= new DialPadEnvironmentSettings();
        }

        return Production ??= new DialPadEnvironmentSettings();
    }

    /// <summary>
    /// Gets the credentials for the active environment selected by <see cref="Environment"/>.
    /// </summary>
    /// <returns>The credentials for the active environment.</returns>
    public DialPadEnvironmentSettings GetActiveEnvironmentSettings()
        => GetEnvironmentSettings(Environment);
}
