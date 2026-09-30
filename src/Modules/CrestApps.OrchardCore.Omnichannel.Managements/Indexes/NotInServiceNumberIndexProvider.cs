using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Indexes;

internal sealed class NotInServiceNumberIndexProvider : IndexProvider<NotInServiceNumber>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotInServiceNumberIndexProvider"/> class.
    /// </summary>
    public NotInServiceNumberIndexProvider()
    {
        CollectionName = OmnichannelConstants.CollectionName;
    }

    public override void Describe(DescribeContext<NotInServiceNumber> context)
    {
        context
            .For<NotInServiceNumberIndex>()
            .Map(number => new NotInServiceNumberIndex
            {
                ItemId = number.ItemId,
                PhoneNumber = number.PhoneNumber,
                Source = number.Source,
                CampaignId = number.CampaignId,
                LastDetectedUtc = number.LastDetectedUtc,
            });
    }
}
