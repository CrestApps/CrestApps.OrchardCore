namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// View model for user segmentation analytics (authenticated vs anonymous breakdown).
/// </summary>
public class ChatAnalyticsUserSegmentViewModel
{
    /// <summary>
    /// Gets or sets the authenticated sessions.
    /// </summary>
    public int AuthenticatedSessions { get; set; }

    /// <summary>
    /// Gets or sets the anonymous sessions.
    /// </summary>
    public int AnonymousSessions { get; set; }

    /// <summary>
    /// Gets or sets the unique authenticated users.
    /// </summary>
    public int UniqueAuthenticatedUsers { get; set; }

    /// <summary>
    /// Gets or sets the unique anonymous visitors.
    /// </summary>
    public int UniqueAnonymousVisitors { get; set; }

    /// <summary>
    /// Gets or sets the authenticated percent.
    /// </summary>
    public double AuthenticatedPercent { get; set; }

    /// <summary>
    /// Gets or sets the anonymous percent.
    /// </summary>
    public double AnonymousPercent { get; set; }
}
