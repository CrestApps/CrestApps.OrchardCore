namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Describes whether the current user is connected to the configured telephony provider and which
/// authentication scenario the provider uses. The soft phone uses this to adapt its UI.
/// </summary>
public sealed class TelephonyConnectionStatus
{
    /// <summary>
    /// Gets or sets the technical name of the configured provider, when one is configured.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a telephony provider is configured and enabled. When
    /// <see langword="false"/> the soft phone cannot place calls and shows an unconfigured state.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the configured provider requires per-user authentication.
    /// When <see langword="false"/> the provider authenticates with shared, account-level credentials.
    /// </summary>
    public bool RequiresAuthentication { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current user is authenticated and able to place calls.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets or sets the authentication scheme the provider uses when per-user authentication is required,
    /// for example <see cref="TelephonyConstants.AuthenticationSchemes.OAuth2"/>. The soft phone uses this value to
    /// select the matching authentication experience, which keeps the widget extensible to new scenarios.
    /// </summary>
    public string AuthenticationScheme { get; set; }
}
