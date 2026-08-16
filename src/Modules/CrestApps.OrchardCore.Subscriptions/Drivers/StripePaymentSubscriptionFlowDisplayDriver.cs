using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Displays the Stripe payment method editor for subscription flows.
/// </summary>
public sealed class StripePaymentSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlowPaymentMethod>
{
    private readonly StripeOptions _stripeOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripePaymentSubscriptionFlowDisplayDriver"/> class.
    /// </summary>
    /// <param name="stripeOptions">The Stripe options used to configure the payment method editor.</param>
    public StripePaymentSubscriptionFlowDisplayDriver(IOptions<StripeOptions> stripeOptions)
    {
        _stripeOptions = stripeOptions.Value;
    }

    /// <summary>
    /// Builds the Stripe payment method editor shape for the Stripe processor group.
    /// </summary>
    /// <param name="method">The payment method model for the current subscription flow.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The display result that renders the Stripe payment method editor.</returns>
    public override IDisplayResult Edit(SubscriptionFlowPaymentMethod method, BuildEditorContext context)
    {
        return Initialize<StripePaymentMethodViewModel>("StripePaymentMethod_Edit", model =>
        {
            model.SessionId = method.Flow.Session.SessionId;
            model.IsLive = _stripeOptions.IsLive;
            model.PublishableKey = _stripeOptions.PublishableKey;
            model.CheckoutMode = _stripeOptions.CheckoutMode;
        }).Location("Content")
        .OnGroup(StripeConstants.ProcessorKey);
    }
}
