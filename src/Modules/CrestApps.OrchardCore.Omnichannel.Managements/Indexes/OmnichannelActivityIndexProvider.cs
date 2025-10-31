using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Indexes;


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
                ItemId = activity.ItemId,
                Channel = activity.Channel,
                ChannelEndpointId = activity.ChannelEndpointId,
                SubjectContentType = activity.SubjectContentType,
                PreferredDestination = activity.PreferredDestination,
                ContactContentType = activity.ContactContentType,
                ContactContentItemId = activity.ContactContentItemId,
                AIProfileName = activity.AIProfileName,
                CampaignId = activity.CampaignId,
                ScheduledUtc = activity.ScheduledUtc,
                Attempts = activity.Attempts,
                AssignedToId = activity.AssignedToId,
                AssignedToUtc = activity.AssignedToUtc,
                CreatedById = activity.CreatedById,
                CompletedUtc = activity.CompletedUtc,
                InteractionType = activity.InteractionType,
                DispositionId = activity.DispositionId,
                CreatedUtc = activity.CreatedUtc,
                UrgencyLevel = activity.UrgencyLevel,
                Status = activity.Status,
            });
    }
}
