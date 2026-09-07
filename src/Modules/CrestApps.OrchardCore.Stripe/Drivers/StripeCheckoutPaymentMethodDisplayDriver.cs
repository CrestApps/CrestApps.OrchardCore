using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.ViewModels;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Stripe.Drivers;

/// <summary>
/// Renders the Stripe panel on the generic checkout payment step: the embedded Payment Element the customer
/// enters their card into, and the client handler that confirms it.
/// </summary>
/// <remarks>
/// Card details are entered directly into Stripe's own iframe and confirmed from the browser, so they never
/// reach this application. That is what keeps the site out of PCI scope, and it is why the payment step needs
/// a client-side confirmation round trip rather than a plain form post.
/// </remarks>
public sealed class StripeCheckoutPaymentMethodDisplayDriver : DisplayDriver<CheckoutFlowPaymentMethod>
{
    private readonly StripeOptions _stripeOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeCheckoutPaymentMethodDisplayDriver"/> class.
    /// </summary>
    /// <param name="stripeOptions">The resolved Stripe options for the tenant.</param>
    public StripeCheckoutPaymentMethodDisplayDriver(IOptions<StripeOptions> stripeOptions)
        => _stripeOptions = stripeOptions.Value;

    /// <inheritdoc/>
    public override IDisplayResult Edit(CheckoutFlowPaymentMethod method, BuildEditorContext context)
        => Initialize<StripeCheckoutPaymentMethodViewModel>("StripeCheckoutPaymentMethod", model =>
        {
            model.SessionId = method.Flow.Session.SessionId;
            model.PublishableKey = _stripeOptions.PublishableKey;
            model.IsLive = _stripeOptions.IsLive;
            model.HasRecurringItems = method.Flow?.Session is not null && method.Flow.Session.TryGet<CheckoutInvoice>(out var invoice) && invoice.GetRecurringGroups().Count > 0;
        })
        .Location("Content")
        .OnGroup(StripeConstants.ProcessorKey);
}
