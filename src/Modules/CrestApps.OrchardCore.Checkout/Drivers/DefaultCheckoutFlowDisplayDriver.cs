using CrestApps.OrchardCore.Checkout.Services;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Drivers;

/// <summary>
/// Renders the chrome shared by every checkout step: the progress stepper, the invoice summary, and the
/// navigation buttons. It is separate from the step drivers so a feature contributing a step never has to
/// re-render the surroundings, and so the customer sees a consistent frame no matter which step they are on.
/// </summary>
public sealed class DefaultCheckoutFlowDisplayDriver : DisplayDriver<CheckoutFlow>
{
    /// <inheritdoc/>
    public override IDisplayResult Edit(CheckoutFlow flow, BuildEditorContext context)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var results = new List<IDisplayResult>
        {
            View("CheckoutFlowStepper", flow).Location("Steps:1"),
            View("CheckoutFlowButtons", flow).Location("Actions:10"),
        };

        // The invoice is what the customer is agreeing to pay, so it is shown alongside every step rather than
        // only at the end: a total that appears for the first time on the payment step reads as a surprise.
        if (flow.Session.TryGet<CheckoutInvoice>(out var invoice))
        {
            results.Add(View("CheckoutInvoiceSummary", invoice).Location("Aside:1"));
        }

        return Combine([.. results]);
    }

    /// <inheritdoc/>
    public override IDisplayResult Display(CheckoutFlow flow, BuildDisplayContext context)
    {
        ArgumentNullException.ThrowIfNull(flow);

        if (!flow.Session.TryGet<CheckoutInvoice>(out var invoice))
        {
            return null;
        }

        return View("CheckoutInvoiceSummary", invoice).Location("Content:10");
    }
}
