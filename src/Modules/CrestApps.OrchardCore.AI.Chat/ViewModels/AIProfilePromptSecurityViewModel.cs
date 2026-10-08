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
    /// Gets or sets the multi-tier sliding-window message limits applied to anonymous callers for this
    /// profile, as one <c>limit, window</c> line per tier. When blank, the site-level tiers are used
    /// unless <see cref="DisableAnonymousMessageRateLimitTiers"/> is set.
    /// </summary>
    public string AnonymousMessageRateLimitTiers { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this profile opts out of tiered anonymous message
    /// limits, falling back to the single message window instead of inheriting the site-level tiers.
    /// </summary>
    public bool DisableAnonymousMessageRateLimitTiers { get; set; }

    /// <summary>
    /// Gets or sets the multi-tier sliding-window session-start limits applied to anonymous callers for
    /// this profile, as one <c>limit, window</c> line per tier. When blank, the site-level tiers are
    /// used unless <see cref="DisableAnonymousSessionStartRateLimitTiers"/> is set.
    /// </summary>
    public string AnonymousSessionStartRateLimitTiers { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this profile opts out of tiered anonymous session-start
    /// limits, falling back to the single session window instead of inheriting the site-level tiers.
    /// </summary>
    public bool DisableAnonymousSessionStartRateLimitTiers { get; set; }

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

    /// <summary>
    /// Gets or sets the site-level anonymous message tiers as a single-line summary, shown as the inherited default.
    /// </summary>
    public string SiteAnonymousMessageRateLimitTiers { get; set; }

    /// <summary>
    /// Gets or sets the site-level anonymous session-start tiers as a single-line summary, shown as the inherited default.
    /// </summary>
    public string SiteAnonymousSessionStartRateLimitTiers { get; set; }
}
