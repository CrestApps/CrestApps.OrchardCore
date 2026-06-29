using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="DialerProfile"/> documents to the <see cref="DialerProfileIndex"/>.
/// </summary>
public sealed class DialerProfileIndexProvider : IndexProvider<DialerProfile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProfileIndexProvider"/> class.
    /// </summary>
    public DialerProfileIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<DialerProfile> context)
    {
        context
            .For<DialerProfileIndex>()
            .Map(profile => new DialerProfileIndex
            {
                ItemId = profile.ItemId,
                Name = profile.Name,
                CampaignId = profile.CampaignId,
                QueueId = profile.QueueId,
                Enabled = profile.Enabled,
            });
    }
}
