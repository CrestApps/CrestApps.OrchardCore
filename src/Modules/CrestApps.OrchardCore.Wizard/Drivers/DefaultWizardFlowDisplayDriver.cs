using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Wizard.Drivers;

/// <summary>
/// Renders the default wizard stepper chrome and navigation controls around the current step. Step drivers
/// contribute the step body into the <c>Content</c> zone while this driver supplies the progress stepper and
/// the previous/next buttons.
/// </summary>
public sealed class DefaultWizardFlowDisplayDriver : DisplayDriver<WizardFlow>
{
    /// <summary>
    /// Builds the confirmation display for a completed wizard.
    /// </summary>
    /// <param name="model">The wizard flow to display.</param>
    /// <param name="context">The display build context.</param>
    /// <returns>The display result that renders the confirmation view.</returns>
    public override Task<IDisplayResult> DisplayAsync(WizardFlow model, BuildDisplayContext context)
    {
        return CombineAsync(
            View("WizardFlowStepper", model).Location("Confirmation", "Steps"),
            View("WizardConfirmation", model).Location("Confirmation", "Content")
        );
    }

    /// <summary>
    /// Builds the default editor chrome for the wizard, including the stepper and navigation buttons.
    /// </summary>
    /// <param name="model">The wizard flow being edited.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The display result that renders the editor chrome.</returns>
    public override Task<IDisplayResult> EditAsync(WizardFlow model, BuildEditorContext context)
    {
        return CombineAsync(
            View("WizardFlowStepper", model).Location("Steps"),

            Initialize<ViewModels.WizardFlowNavigation>("WizardFlowButtons", vm =>
            {
                vm.SessionId = model.Session.SessionId;
                vm.WizardType = model.Session.WizardType;
                vm.PreviousStep = model.GetPreviousStep()?.Key;
                vm.CurrentStep = model.GetCurrentStep()?.Key;
                vm.NextStep = model.GetNextStep()?.Key;
            }).Location("Actions")
        );
    }
}
