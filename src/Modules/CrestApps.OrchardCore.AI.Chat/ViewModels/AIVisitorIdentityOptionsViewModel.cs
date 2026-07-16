using CrestApps.Core.AI.Security;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the site settings editor model for anonymous AI chat visitor identity.
/// </summary>
public sealed class AIVisitorIdentityOptionsViewModel
{
    /// <summary>
    /// Gets or sets the cookie name.
    /// </summary>
    public string CookieName { get; set; }

    /// <summary>
    /// Gets or sets the cookie lifetime in days.
    /// </summary>
    public int CookieLifetimeDays { get; set; }

    /// <summary>
    /// Gets or sets the remote-address mode.
    /// </summary>
    public AIVisitorRemoteAddressMode RemoteAddressMode { get; set; }

    /// <summary>
    /// Gets or sets the remote-address hash salt.
    /// </summary>
    public string RemoteAddressHashSalt { get; set; }
}
