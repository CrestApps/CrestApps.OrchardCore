using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class SubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    public override Task<IDisplayResult> DisplayAsync(SubscriptionFlow model, BuildDisplayContext context)
    {
        return Task.FromResult<IDisplayResult>(
            Combine(
                View("SubscriptionFlowSteps", model)
                .Location("Confirmation", "Steps"),

                View("SubscriptionConfirmation", model)
                .Location("Confirmation", "Content")
            )
        );
    }

    public override Task<IDisplayResult> EditAsync(SubscriptionFlow model, BuildEditorContext context)
    {
        return Task.FromResult<IDisplayResult>(
            Combine(
                View("SubscriptionFlowSteps", model).Location("Steps"),

                View("SubscriptionInformation", model).Location("Content:before"),

                Initialize<SubscriptionFlowNavigation>("SubscriptionFlowButtons", vm =>
                {
                    vm.Direction = model.Direction;
                    vm.PreviousStep = model.GetPreviousStep()?.Key;
                    vm.CurrentStep = model.GetCurrentStep()?.Key;
                    vm.NextStep = model.GetNextStep()?.Key;
                    vm.IsPaymentStep = model.CurrentStepEquals(PaymentSubscriptionHandler.StepKey);
                }).Location("Content:after")
            )
        );
    }

    public override async Task<IDisplayResult> UpdateAsync(SubscriptionFlow model, UpdateEditorContext context)
    {
        var vm = new SubscriptionFlowNavigation();

        await context.Updater.TryUpdateModelAsync(vm, Prefix);

        model.Direction = vm.Direction;

        return await EditAsync(model, context);
    }
}
