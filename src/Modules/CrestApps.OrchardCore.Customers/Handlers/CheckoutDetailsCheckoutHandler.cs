using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Services;

namespace CrestApps.OrchardCore.Customers.Handlers;

/// <summary>
/// Shows the Contact step whenever the store has checkout fields to ask for. A signed-in buyer who is not asked for a
/// billing address otherwise skips the step, and would never see them.
/// </summary>
/// <remarks>
/// This runs once every handler has initialized the checkout, so it wins over the Contact step's own decision.
/// </remarks>
internal sealed class CheckoutDetailsCheckoutHandler : CheckoutHandlerBase
{
    private readonly CheckoutDetailsForm _form;

    public CheckoutDetailsCheckoutHandler(CheckoutDetailsForm form)
    {
        _form = form;
    }

    public override async Task InitializedAsync(CheckoutFlowInitializedContext context)
    {
        var step = context.Flow.Session.Steps.FirstOrDefault(step => string.Equals(step.Key, CustomersConstants.ContactStepKey, StringComparison.Ordinal));

        if (step is { Conceal: true } && await _form.HasFieldsAsync())
        {
            step.Conceal = false;
        }
    }
}
