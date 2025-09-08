using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

public sealed class OmnichannelActivityIndex : MapIndex
{
    public string ActivityId { get; set; }

    public string Channel { get; set; }

    public string ChannelEndpoint { get; set; }

    public string PreferredDestination { get; set; }

    public string AIProfileId { get; set; }

    public string ContactContentItemId { get; set; }

    public string ContactContentType { get; set; }

    public string CampaignId { get; set; }

    public string SubjectId { get; set; }

    public DateTime ScheduledAt { get; set; }

    public int Attempts { get; set; }

    public string AssignedToId { get; set; }

    public DateTime? AssignedToUtc { get; set; }

    public string CreatedById { get; set; }

    public string Disposition { get; set; }

    public DateTime CreatedUtc { get; set; }

    public UrgencyLevel UrgencyLevel { get; set; }

    public ActivityStatus Status { get; set; }
}
