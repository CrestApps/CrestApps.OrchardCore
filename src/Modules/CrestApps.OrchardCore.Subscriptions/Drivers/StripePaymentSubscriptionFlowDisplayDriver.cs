using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class StripePaymentSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlowPaymentMethod>
{
    private readonly StripeOptions _stripeOptions;

    public StripePaymentSubscriptionFlowDisplayDriver(IOptions<StripeOptions> stripeOptions)
    {
        _stripeOptions = stripeOptions.Value;
    }

    public override IDisplayResult Edit(SubscriptionFlowPaymentMethod method, BuildEditorContext context)
    {
        return Initialize<StripePaymentMethodViewModel>("StripePaymentMethod_Edit", model =>
        {
            model.SessionId = method.Flow.Session.SessionId;
            model.IsLive = _stripeOptions.IsLive;
            model.PublishableKey = _stripeOptions.PublishableKey;
        }).Location("Content")
        .OnGroup(StripeConstants.ProcessorKey);
    }
}
