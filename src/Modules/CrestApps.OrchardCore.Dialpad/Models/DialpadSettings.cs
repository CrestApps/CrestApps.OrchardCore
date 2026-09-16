namespace CrestApps.OrchardCore.Dialpad.Models;

/// <summary>
/// Represents the Dialpad provider site settings. Production and sandbox credentials are configured
/// independently, and the active <see cref="Environment"/> selects which credential set the provider uses
/// when connecting.
/// </summary>
public sealed class DialpadSettings
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
    /// Gets or sets the credentials and options for the production Dialpad environment.
    /// </summary>
    public DialpadEnvironmentSettings Production { get; set; } = new();

    /// <summary>
    /// Gets or sets the credentials and options for the sandbox Dialpad environment.
    /// </summary>
    public DialpadEnvironmentSettings Sandbox { get; set; } = new();

    /// <summary>
    /// Gets the credentials for the specified Dialpad environment, creating an empty set when none exists.
    /// </summary>
    /// <param name="environment">The environment whose credentials are requested.</param>
    /// <returns>The credentials for the environment.</returns>
    public DialpadEnvironmentSettings GetEnvironmentSettings(DialpadEnvironment environment)
    {
        if (environment == DialpadEnvironment.Sandbox)
        {
            return Sandbox ??= new DialpadEnvironmentSettings();
        }

        return Production ??= new DialpadEnvironmentSettings();
    }

    /// <summary>
    /// Gets the credentials for the active environment selected by <see cref="Environment"/>.
    /// </summary>
    /// <returns>The credentials for the active environment.</returns>
    public DialpadEnvironmentSettings GetActiveEnvironmentSettings()
        => GetEnvironmentSettings(Environment);
}
