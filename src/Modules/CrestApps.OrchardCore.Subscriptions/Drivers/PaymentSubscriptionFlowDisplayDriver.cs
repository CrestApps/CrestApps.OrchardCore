using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class PaymentSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    public override Task<IDisplayResult> EditAsync(SubscriptionFlow flow, BuildEditorContext context)
    {
        if (!flow.CurrentStepEquals(PaymentSubscriptionHandler.StepKey))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return Task.FromResult<IDisplayResult>(
            Initialize<Invoice>("SubscriptionPayments_Edit", model =>
            {
                var lineItems = new List<InvoiceLineItem>();

                foreach (var step in flow.GetSortedSteps())
                {
                    if (step.Payment == null)
                    {
                        // Steps with no payment information can be ignored.
                        continue;
                    }

                    var lineItem = new InvoiceLineItem()
                    {
                        Description = step.Title,
                        Quantity = 1,
                        UnitPrice = step.Payment.BillingAmount,
                        DueNow = step.Payment.InitialAmount,
                        BillingDuration = step.Payment.BillingDuration,
                        BillingCycleLimit = step.Payment.BillingCycleLimit,
                        SubscriptionDayDelay = step.Payment.SubscriptionDayDelay,
                    };

                    lineItem.Subtotal = lineItem.Quantity * lineItem.UnitPrice;

                    lineItems.Add(lineItem);
                }

                model.LineItems = lineItems.ToArray();
            }).Location("Content")
        );
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
