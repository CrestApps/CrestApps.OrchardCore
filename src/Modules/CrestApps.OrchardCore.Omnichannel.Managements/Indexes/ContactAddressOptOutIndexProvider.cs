using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Indexes;

internal sealed class ContactAddressOptOutIndexProvider : IndexProvider<ContactAddressOptOut>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactAddressOptOutIndexProvider"/> class.
    /// </summary>
    public ContactAddressOptOutIndexProvider()
    {
        CollectionName = OmnichannelConstants.CollectionName;
    }

    public override void Describe(DescribeContext<ContactAddressOptOut> context)
    {
        context
            .For<ContactAddressOptOutIndex>()
            .Map(optOut => new ContactAddressOptOutIndex
            {
                ItemId = optOut.ItemId,
                Channel = optOut.Channel,
                Address = optOut.Address,
            });
    }
}
