using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Wizard.Core.Drivers;

/// <summary>
/// A display driver base class for a single wizard step. It runs only when the flow's current step matches
/// <see cref="StepKey"/> and, when <see cref="WizardType"/> is set, only for that wizard type. This lets a
/// code wizard contribute one strongly-typed driver per step without inspecting the flow in every driver.
/// </summary>
public abstract class WizardFlowDisplayDriver : DisplayDriver<WizardFlow>
{
    /// <summary>
    /// Gets the wizard step key handled by the driver.
    /// </summary>
    protected abstract string StepKey { get; }

    /// <summary>
    /// Gets the wizard type this driver is scoped to, or <see langword="null"/> to run for the step
    /// regardless of wizard type.
    /// </summary>
    protected virtual string WizardType => null;

    private bool Handles(WizardFlow flow)
        => flow.CurrentStepEquals(StepKey) &&
            (WizardType == null || string.Equals(flow.Session.WizardType, WizardType, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Builds the display result for the current wizard step.
    /// </summary>
    /// <param name="flow">The wizard flow being displayed.</param>
    /// <param name="context">The display build context.</param>
    /// <returns>The display result for the handled step, or <see langword="null"/> when another step is current.</returns>
    public sealed override Task<IDisplayResult> DisplayAsync(WizardFlow flow, BuildDisplayContext context)
    {
        if (!Handles(flow))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return DisplayStepAsync(flow, context);
    }

    /// <summary>
    /// Synchronous display is not supported for wizard step drivers.
    /// </summary>
    /// <param name="model">The wizard flow being displayed.</param>
    /// <param name="context">The display build context.</param>
    /// <returns>This method does not return because it always throws.</returns>
    public sealed override IDisplayResult Display(WizardFlow model, BuildDisplayContext context)
        => throw new NotImplementedException();

    /// <summary>
    /// Builds the asynchronous display result for the handled wizard step.
    /// </summary>
    /// <param name="flow">The wizard flow being displayed.</param>
    /// <param name="context">The display build context.</param>
    /// <returns>The display result for the handled step.</returns>
    protected virtual Task<IDisplayResult> DisplayStepAsync(WizardFlow flow, BuildDisplayContext context)
        => Task.FromResult(DisplayStep(flow, context));

    /// <summary>
    /// Builds the synchronous display result for the handled wizard step.
    /// </summary>
    /// <param name="flow">The wizard flow being displayed.</param>
    /// <param name="context">The display build context.</param>
    /// <returns>The display result for the handled step, or <see langword="null"/> when no display shape is produced.</returns>
    protected virtual IDisplayResult DisplayStep(WizardFlow flow, BuildDisplayContext context)
        => null;

    /// <summary>
    /// Builds the editor result for the current wizard step.
    /// </summary>
    /// <param name="flow">The wizard flow being edited.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The editor result for the handled step, or <see langword="null"/> when another step is current.</returns>
    public sealed override Task<IDisplayResult> EditAsync(WizardFlow flow, BuildEditorContext context)
    {
        if (!Handles(flow))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return EditStepAsync(flow, context);
    }

    /// <summary>
    /// Synchronous edit is not supported for wizard step drivers.
    /// </summary>
    /// <param name="model">The wizard flow being edited.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>This method does not return because it always throws.</returns>
    public sealed override IDisplayResult Edit(WizardFlow model, BuildEditorContext context)
        => throw new NotImplementedException();

    /// <summary>
    /// Builds the asynchronous editor result for the handled wizard step.
    /// </summary>
    /// <param name="flow">The wizard flow being edited.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The editor result for the handled step.</returns>
    protected virtual Task<IDisplayResult> EditStepAsync(WizardFlow flow, BuildEditorContext context)
        => Task.FromResult(EditStep(flow, context));

    /// <summary>
    /// Builds the synchronous editor result for the handled wizard step.
    /// </summary>
    /// <param name="flow">The wizard flow being edited.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The editor result for the handled step, or <see langword="null"/> when no editor shape is produced.</returns>
    protected virtual IDisplayResult EditStep(WizardFlow flow, BuildEditorContext context)
        => null;

    /// <summary>
    /// Updates the model for the current wizard step.
    /// </summary>
    /// <param name="flow">The wizard flow being updated.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The editor result for the handled step, or <see langword="null"/> when another step is current.</returns>
    public sealed override Task<IDisplayResult> UpdateAsync(WizardFlow flow, UpdateEditorContext context)
    {
        if (!Handles(flow))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return UpdateStepAsync(flow, context);
    }

    /// <summary>
    /// Updates the model for the handled wizard step.
    /// </summary>
    /// <param name="flow">The wizard flow being updated.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The editor result for the handled step.</returns>
    protected virtual Task<IDisplayResult> UpdateStepAsync(WizardFlow flow, UpdateEditorContext context)
        => EditAsync(flow, context);
}
