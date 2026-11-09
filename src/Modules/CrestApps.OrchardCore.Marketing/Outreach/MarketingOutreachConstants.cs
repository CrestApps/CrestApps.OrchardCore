namespace CrestApps.OrchardCore.Marketing.Outreach;

/// <summary>
/// The names Marketing registers with the outreach engine.
/// </summary>
public static class MarketingOutreachConstants
{
    /// <summary>
    /// The owner type of the runs that send campaigns.
    /// </summary>
    public const string CampaignOwnerType = "MarketingCampaign";

    /// <summary>
    /// The audience source that pages a campaign's segment.
    /// </summary>
    public const string SegmentAudienceSource = "MarketingSegment";

    /// <summary>
    /// The renderer that renders a campaign's Liquid for a customer, with their unsubscribe link.
    /// </summary>
    public const string CampaignRenderer = "MarketingCampaign";

    /// <summary>
    /// The customers a batch sends to.
    /// </summary>
    public const int PageSize = 50;
}
