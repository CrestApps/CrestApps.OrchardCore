using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class StripePaymentSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    public readonly StripeOptions _stripeOptions;

    public StripePaymentSubscriptionFlowDisplayDriver(IOptions<StripeOptions> stripeOptions)
    {
        _stripeOptions = stripeOptions.Value;
    }

    public override IDisplayResult Edit(SubscriptionFlow flow, BuildEditorContext context)
    {
        if (!flow.CurrentStepEquals(PaymentSubscriptionHandler.StepKey))
        {
            return null;
        }

        return Initialize<StripeViewModel>("StripeSubscriptionPayments_Edit", model =>
        {
            model.SessionId = flow.Session.SessionId;
            model.IsLive = _stripeOptions.IsLive;
            model.PublishableKey = _stripeOptions.PublishableKey;
        }).Location("Content:after");
    }

    public override async Task<IDisplayResult> UpdateAsync(SubscriptionFlow model, UpdateEditorContext context)
    {
        var vm = new SubscriptionFlowNavigation();

        // Don't use a prefix for Direction.
        await context.Updater.TryUpdateModelAsync(vm, prefix: string.Empty);

        model.Direction = vm.Direction;

        return Edit(model, context);
    }
}
