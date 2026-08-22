using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Subscriptions.Drivers.Steps;

/// <summary>
/// Displays the default subscription flow stepper, confirmation summary, and navigation controls.
/// </summary>
public sealed class DefaultSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSubscriptionFlowDisplayDriver"/> class.
    /// </summary>
    /// <param name="documentJsonSerializerOptions">The JSON serializer options used to read confirmation data from the flow session.</param>
    public DefaultSubscriptionFlowDisplayDriver(IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions)
    {
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
    }

    /// <summary>
    /// Builds the confirmation display with the flow stepper and subscription confirmation details.
    /// </summary>
    /// <param name="model">The subscription flow to display.</param>
    /// <param name="context">The display build context.</param>
    /// <returns>The display result that renders the confirmation view.</returns>
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

    /// <summary>
    /// Builds the default editor chrome for the subscription flow, including the stepper, information header, and navigation buttons.
    /// </summary>
    /// <param name="model">The subscription flow being edited.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The display result that renders the editor chrome.</returns>
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

    /// <summary>
    /// Updates navigation data submitted with the subscription flow and rebuilds the editor chrome.
    /// </summary>
    /// <param name="model">The subscription flow being updated.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The display result that renders the updated editor chrome.</returns>
    public override async Task<IDisplayResult> UpdateAsync(SubscriptionFlow model, UpdateEditorContext context)
    {
        var vm = new SubscriptionFlowNavigation();

        await context.Updater.TryUpdateModelAsync(vm, Prefix);

        return await EditAsync(model, context);
    }
}
