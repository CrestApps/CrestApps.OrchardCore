using CrestApps.OrchardCore.Checkout.Services;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Checkout.Drivers;

/// <summary>
/// Renders the test payment panel on the checkout payment step: one line saying that no money moves.
/// </summary>
/// <remarks>
/// The panel asks for nothing, because the test provider needs nothing from the browser. It still exists so the
/// person testing can never mistake the test method for a real one.
/// </remarks>
public sealed class TestCheckoutPaymentMethodDisplayDriver : DisplayDriver<CheckoutFlowPaymentMethod>
{
    /// <inheritdoc/>
    public override IDisplayResult Edit(CheckoutFlowPaymentMethod method, BuildEditorContext context)
        => View("TestCheckoutPaymentMethod", method)
            .Location("Content")
            .OnGroup(TestCheckoutPaymentProvider.ProviderKey);
}
