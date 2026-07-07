namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// Represents the tenant-configured Asterisk provider site settings.
/// </summary>
public sealed class AsteriskSettings : AsteriskConnectionSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the tenant-configured Asterisk provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the protected ARI password. The value is stored encrypted using the data protection provider.
    /// </summary>
    public string Password { get; set; }
}
