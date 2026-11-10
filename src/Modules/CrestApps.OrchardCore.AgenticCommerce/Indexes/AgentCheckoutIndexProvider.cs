using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.AgenticCommerce.Core.Indexes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
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
            .Map(checkout => new AgentCheckoutIndex
            {
                ItemId = checkout.ItemId,
                PlatformId = checkout.PlatformId,
                Protocol = checkout.Protocol,
                IsTerminal = checkout.IsTerminal,
                CheckoutSessionId = checkout.CheckoutSessionId,
                OrderId = checkout.OrderId,
                ExpiresUtc = checkout.ExpiresUtc,
            });
    }
}
