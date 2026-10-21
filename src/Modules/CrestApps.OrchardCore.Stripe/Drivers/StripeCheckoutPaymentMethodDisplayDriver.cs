using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Services;
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
    private readonly IOptionsMonitor<StripeOptions> _stripeOptions;
    private readonly IStripeWalletService _walletService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeCheckoutPaymentMethodDisplayDriver"/> class.
    /// </summary>
    /// <param name="stripeOptions">The Stripe options for the tenant, read when the editor is built.</param>
    /// <param name="walletService">The signed-in buyer's saved cards.</param>
    public StripeCheckoutPaymentMethodDisplayDriver(
        IOptionsMonitor<StripeOptions> stripeOptions,
        IStripeWalletService walletService)
    {
        _stripeOptions = stripeOptions;
        _walletService = walletService;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(CheckoutFlowPaymentMethod method, BuildEditorContext context)
        => Initialize<StripeCheckoutPaymentMethodViewModel>("StripeCheckoutPaymentMethod", async model =>
        {
            var stripeOptions = _stripeOptions.CurrentValue;

            model.SessionId = method.Flow.Session.SessionId;
            model.PublishableKey = stripeOptions.PublishableKey;
            model.IsLive = stripeOptions.IsLive;
            model.SavePaymentMethod = method.SavePaymentMethod;

            if (method.Flow?.Session is not null && method.Flow.Session.TryGet<CheckoutInvoice>(out var invoice))
            {
                model.HasRecurringItems = invoice.GetRecurringGroups().Count > 0;

                var currency = string.IsNullOrEmpty(invoice.Currency) ? method.Flow.Session.Currency : invoice.Currency;

                // A wallet sheet must show what is being paid, so the buttons are offered only when there is a
                // positive amount due now to show.
                if (invoice.AmountDue > 0m && !string.IsNullOrEmpty(currency))
                {
                    model.WalletAmount = StripeCurrency.ToMinorUnits(invoice.AmountDue, currency);
                    model.WalletCurrency = currency.ToLowerInvariant();
                }
            }

            // A signed-in buyer paying for a one-time purchase can keep the card, or pay with one they kept. A
            // subscription or installments keep their card for later charges on their own terms.
            var ownerId = method.Flow?.Session?.OwnerId;

            model.CanUseSavedCards = !string.IsNullOrEmpty(ownerId) && !model.HasRecurringItems && !model.SavePaymentMethod;

            if (model.CanUseSavedCards && !string.IsNullOrEmpty(stripeOptions.PublishableKey))
            {
                model.SavedCards = await _walletService.ListCardsAsync(ownerId);
            }
        })
        .Location("Content")
        .OnGroup(StripeConstants.ProcessorKey);
}
