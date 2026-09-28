namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// Represents the configuration-backed default Asterisk provider options loaded from shell configuration.
/// </summary>
public sealed class DefaultAsteriskOptions : AsteriskConnectionSettings
{
    /// <summary>
    /// Gets or sets the ARI password from shell configuration.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the configuration contains enough information to expose the provider.
    /// </summary>
    public bool IsEnabled { get; set; }
}
