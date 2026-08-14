using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Subscriptions.Drivers.Steps;

public sealed class DefaultSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    public DefaultSubscriptionFlowDisplayDriver(IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions)
    {
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
    }

    public override Task<IDisplayResult> DisplayAsync(SubscriptionFlow model, BuildDisplayContext context)
    {
        return CombineAsync(
            View("SubscriptionFlowStepper", model)
            .Location("Confirmation", "Steps"),

            Initialize<SubscriptionConfirmationViewModel>("SubscriptionConfirmation", vm =>
            {
                var confirmation = SubscriptionConfirmationViewModel.Create(model.Session, _documentJsonSerializerOptions.SerializerOptions);

                vm.Invoice = confirmation.Invoice;
                vm.Subscriptions = confirmation.Subscriptions;
                vm.TenantOnboarding = confirmation.TenantOnboarding;
            })
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
