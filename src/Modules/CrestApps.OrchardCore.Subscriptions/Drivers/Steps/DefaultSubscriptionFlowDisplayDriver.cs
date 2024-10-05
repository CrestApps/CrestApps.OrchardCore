using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers.Steps;

public sealed class DefaultSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    public override Task<IDisplayResult> DisplayAsync(SubscriptionFlow model, BuildDisplayContext context)
    {
        return CombineAsync(
            View("SubscriptionFlowStepper", model)
            .Location("Confirmation", "Steps"),

            View("SubscriptionConfirmation", model)
            .Location("Confirmation", "Content")
        );
    }

    public override Task<IDisplayResult> EditAsync(SubscriptionFlow model, BuildEditorContext context)
    {
        return CombineAsync(
            View("SubscriptionFlowStepper", model).Location("Steps"),

            View("SubscriptionInformation", model).Location("Header"),

            Initialize<SubscriptionFlowNavigation>("SubscriptionFlowButtons", vm =>
            {
                vm.SessionId = model.Session.SessionId;
                vm.PreviousStep = model.GetPreviousStep()?.Key;
                vm.CurrentStep = model.GetCurrentStep()?.Key;
                vm.NextStep = model.GetNextStep()?.Key;
                vm.IsPaymentStep = model.CurrentStepEquals(SubscriptionConstants.StepKey.Payment);
            }).Location("Actions")
        );
    }

    public override async Task<IDisplayResult> UpdateAsync(SubscriptionFlow model, UpdateEditorContext context)
    {
        var vm = new SubscriptionFlowNavigation();

        await context.Updater.TryUpdateModelAsync(vm, Prefix);

        return await EditAsync(model, context);
    }
}
