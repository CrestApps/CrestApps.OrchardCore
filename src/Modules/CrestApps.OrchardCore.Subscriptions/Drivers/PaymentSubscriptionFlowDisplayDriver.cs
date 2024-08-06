using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class PaymentSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    public override IDisplayResult Edit(SubscriptionFlow flow, BuildEditorContext context)
    {
        if (!flow.CurrentStepEquals(PaymentSubscriptionHandler.StepKey))
        {
            return null;
        }

        return View("SubscriptionPayments_Edit", flow.Session.As<Invoice>())
            .Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(SubscriptionFlow model, UpdateEditorContext context)
    {
        var vm = new SubscriptionFlowNavigation();

        // Don't use a prefix for Direction.
        await context.Updater.TryUpdateModelAsync(vm, prefix: string.Empty);

        model.Direction = vm.Direction;

        return await EditAsync(model, context);
    }
}
