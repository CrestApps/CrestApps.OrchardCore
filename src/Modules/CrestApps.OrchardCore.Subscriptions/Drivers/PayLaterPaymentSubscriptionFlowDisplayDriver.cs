using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Displays the Pay Later payment method editor for subscription flows.
/// </summary>
public sealed class PayLaterPaymentSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlowPaymentMethod>
{
    /// <summary>
    /// Builds the Pay Later payment method editor shape for the Pay Later processor group.
    /// </summary>
    /// <param name="method">The payment method model for the current subscription flow.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The display result that renders the Pay Later payment method editor.</returns>
    public override IDisplayResult Edit(SubscriptionFlowPaymentMethod method, BuildEditorContext context)
    {
        return View("PayLaterPaymentMethod_Edit", method)
            .Location("Content")
            .OnGroup(SubscriptionConstants.PayLaterProcessorKey);
    }
}
