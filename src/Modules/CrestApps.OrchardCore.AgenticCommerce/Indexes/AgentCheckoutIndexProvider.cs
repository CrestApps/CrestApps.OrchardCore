using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.AgenticCommerce.Core.Indexes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AgenticCommerce.Indexes;

internal sealed class AgentCheckoutIndexProvider : IndexProvider<AgentCheckout>
{
    public AgentCheckoutIndexProvider()
    {
        CollectionName = AgenticCommerceStorage.CollectionName;
    }

    public override void Describe(DescribeContext<AgentCheckout> context)
    {
        context
            .For<AgentCheckoutIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(checkout => new AgentCheckoutIndex
            {
                ItemId = checkout.ItemId,
                PlatformId = checkout.PlatformId,

                // Only unregistered platforms share their part of the allowance by the origin of their profile.
                PlatformOrigin = checkout.PlatformId?.StartsWith(AgentPlatform.UnregisteredIdPrefix, StringComparison.Ordinal) == true
                    ? AgentPlatformAddress.GetOriginKey(checkout.GetProperty(AgentCheckout.ProfileUrlProperty))
                    : null,
                Protocol = checkout.Protocol,
                IsTerminal = checkout.IsTerminal,
                CheckoutSessionId = checkout.CheckoutSessionId,
                OrderId = checkout.OrderId,
                ExpiresUtc = checkout.ExpiresUtc,
            });
    }
}
