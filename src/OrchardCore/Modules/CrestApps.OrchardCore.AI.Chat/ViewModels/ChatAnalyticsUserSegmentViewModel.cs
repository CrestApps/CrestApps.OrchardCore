namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// View model for user segmentation analytics (authenticated vs anonymous breakdown).
/// </summary>
public class ChatAnalyticsUserSegmentViewModel
{
    public int AuthenticatedSessions { get; set; }

    public int AnonymousSessions { get; set; }

    public int UniqueAuthenticatedUsers { get; set; }

    public int UniqueAnonymousVisitors { get; set; }

    public double AuthenticatedPercent { get; set; }

    public double AnonymousPercent { get; set; }
}
