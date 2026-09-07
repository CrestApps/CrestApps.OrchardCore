using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.PayLater.Services;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.PayLater.Drivers;

/// <summary>
/// Renders the Pay Later panel on the checkout payment step.
/// </summary>
/// <remarks>
/// Pay Later collects nothing, so the panel is purely an explanation of what the customer is agreeing to. It
/// still exists as a driver rather than being special-cased in the checkout page, because that is what keeps
/// the payment step provider-agnostic: adding a gateway later means adding a driver, not editing the page.
/// </remarks>
public sealed class PayLaterCheckoutPaymentMethodDisplayDriver : DisplayDriver<CheckoutFlowPaymentMethod>
{
    /// <inheritdoc/>
    public override IDisplayResult Edit(CheckoutFlowPaymentMethod method, BuildEditorContext context)
        => View("PayLaterCheckoutPaymentMethod", method)
            .Location("Content")
            .OnGroup(PayLaterCheckoutPaymentProvider.ProcessorKey);
}
