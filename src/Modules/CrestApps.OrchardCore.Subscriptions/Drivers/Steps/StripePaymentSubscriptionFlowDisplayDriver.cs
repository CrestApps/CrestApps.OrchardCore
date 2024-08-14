using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers.Steps;

public sealed class StripePaymentSubscriptionFlowDisplayDriver : SubscriptionFlowDisplayDriver
{
    public readonly StripeOptions _stripeOptions;

    public StripePaymentSubscriptionFlowDisplayDriver(IOptions<StripeOptions> stripeOptions)
    {
        _stripeOptions = stripeOptions.Value;
    }

    protected override string StepKey
        => SubscriptionConstants.StepKey.Payment;

    protected override IDisplayResult EditStep(SubscriptionFlow flow, BuildEditorContext context)
    {
        return Initialize<StripePaymentStepViewModel>("StripePaymentStep_Edit", model =>
        {
            model.SessionId = flow.Session.SessionId;
            model.IsLive = _stripeOptions.IsLive;
            model.PublishableKey = _stripeOptions.PublishableKey;
        }).Location("Content:after");
    }
}
