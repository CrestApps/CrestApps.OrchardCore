using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class PayLaterPaymentSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlowPaymentMethod>
{
    public override IDisplayResult Edit(SubscriptionFlowPaymentMethod method, BuildEditorContext context)
    {
        return View("PayLaterPaymentMethod_Edit", method)
            .Location("Content")
            .OnGroup(SubscriptionConstants.PayLaterProcessorKey);
    }
}
