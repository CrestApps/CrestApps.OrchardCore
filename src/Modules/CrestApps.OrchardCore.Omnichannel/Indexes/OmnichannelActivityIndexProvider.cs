using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Indexes;


public sealed class OmnichannelActivityIndexProvider : IndexProvider<OmnichannelActivity>
{
    public OmnichannelActivityIndexProvider()
    {
        CollectionName = OmnichannelConstants.CollectionName;
    }

    public override void Describe(DescribeContext<OmnichannelActivity> context)
    {
        context
            .For<OmnichannelActivityIndex>()
            .Map(activity => new OmnichannelActivityIndex()
            {
                ActivityId = activity.Id,
                Channel = activity.Channel,
                ChannelEndpoint = activity.ChannelEndpoint,
                SubjectId = activity.SubjectId,
                PreferredDestination = activity.PreferredDestination,
                ContactContentItemId = activity.ContactContentItemId,
                ContactContentType = activity.ContactContentType,
                CampaignId = activity.CampaignId,
                ScheduledAt = activity.ScheduledAt,
                Attempts = activity.Attempts,
                AssignedToId = activity.AssignedToId,
                AssignedToUtc = activity.AssignedToUtc,
                CreatedById = activity.CreatedById,
                Disposition = activity.DispositionId,
                CreatedUtc = activity.CreatedUtc,
                UrgencyLevel = activity.UrgencyLevel,
                Status = activity.Status,
            });
    }
}
