namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the per-profile anti-spam throttle override editor model.
/// Each value is optional; when left blank the site-wide prompt security default is used.
/// </summary>
public class AIProfilePromptSecurityViewModel
{
    /// <summary>
    /// Gets or sets the maximum number of messages allowed within the rate-limit window for this profile.
    /// When <see langword="null"/>, the site-level default is used. Set to <c>0</c> to disable message throttling.
    /// </summary>
    public int? MaxMessagesPerWindow { get; set; }

    /// <summary>
    /// Gets or sets the message rate-limit window length in seconds for this profile.
    /// When <see langword="null"/>, the site-level default is used.
    /// </summary>
    public int? RateLimitWindowSeconds { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of anonymous chat sessions that can be started
    /// within the anonymous session-start window for this profile.
    /// When <see langword="null"/>, the site-level default is used. Set to <c>0</c> to disable anonymous session-start throttling.
    /// </summary>
    public int? MaxAnonymousSessionsPerWindow { get; set; }

    /// <summary>
    /// Gets or sets the anonymous session-start rate-limit window length in seconds for this profile.
    /// When <see langword="null"/>, the site-level default is used.
    /// </summary>
    public int? AnonymousSessionRateLimitWindowSeconds { get; set; }

    /// <summary>
    /// Gets or sets the site-level maximum messages per window, shown as the inherited default.
    /// </summary>
    public int SiteMaxMessagesPerWindow { get; set; }

    /// <summary>
    /// Gets or sets the site-level message rate-limit window in seconds, shown as the inherited default.
    /// </summary>
    public int SiteRateLimitWindowSeconds { get; set; }

    /// <summary>
    /// Gets or sets the site-level maximum anonymous sessions per window, shown as the inherited default.
    /// </summary>
    public int SiteMaxAnonymousSessionsPerWindow { get; set; }

    /// <summary>
    /// Gets or sets the site-level anonymous session-start window in seconds, shown as the inherited default.
    /// </summary>
    public int SiteAnonymousSessionRateLimitWindowSeconds { get; set; }
}
