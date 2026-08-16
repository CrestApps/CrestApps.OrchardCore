using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Core;

/// <summary>
/// Provides a display driver base class that runs only for the current subscription flow step.
/// </summary>
public abstract class SubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    /// <summary>
    /// Gets the subscription flow step key handled by the driver.
    /// </summary>
    protected abstract string StepKey { get; }

    /// <summary>
    /// Builds the display result for the current subscription flow step.
    /// </summary>
    /// <param name="flow">The subscription flow being displayed.</param>
    /// <param name="context">The display build context.</param>
    /// <returns>The display result for the handled step, or <see langword="null"/> when another step is current.</returns>
    public sealed override Task<IDisplayResult> DisplayAsync(SubscriptionFlow flow, BuildDisplayContext context)
    {
        if (!flow.CurrentStepEquals(StepKey))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return DisplayStepAsync(flow, context);
    }

    /// <summary>
    /// Synchronous display is not supported for subscription flow step drivers.
    /// </summary>
    /// <param name="model">The subscription flow being displayed.</param>
    /// <param name="context">The display build context.</param>
    /// <returns>This method does not return because it always throws.</returns>
    public sealed override IDisplayResult Display(SubscriptionFlow model, BuildDisplayContext context)
        => throw new NotImplementedException();

    /// <summary>
    /// Builds the asynchronous display result for the handled subscription flow step.
    /// </summary>
    /// <param name="flow">The subscription flow being displayed.</param>
    /// <param name="context">The display build context.</param>
    /// <returns>The display result for the handled step.</returns>
    protected virtual Task<IDisplayResult> DisplayStepAsync(SubscriptionFlow flow, BuildDisplayContext context)
    {
        return Task.FromResult(DisplayStep(flow, context));
    }

    /// <summary>
    /// Builds the synchronous display result for the handled subscription flow step.
    /// </summary>
    /// <param name="flow">The subscription flow being displayed.</param>
    /// <param name="context">The display build context.</param>
    /// <returns>The display result for the handled step, or <see langword="null"/> when no display shape is produced.</returns>
    protected virtual IDisplayResult DisplayStep(SubscriptionFlow flow, BuildDisplayContext context)
    {
        return null;
    }

    /// <summary>
    /// Builds the editor result for the current subscription flow step.
    /// </summary>
    /// <param name="flow">The subscription flow being edited.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The editor result for the handled step, or <see langword="null"/> when another step is current.</returns>
    public sealed override Task<IDisplayResult> EditAsync(SubscriptionFlow flow, BuildEditorContext context)
    {
        if (!flow.CurrentStepEquals(StepKey))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return EditStepAsync(flow, context);
    }

    /// <summary>
    /// Synchronous edit is not supported for subscription flow step drivers.
    /// </summary>
    /// <param name="model">The subscription flow being edited.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>This method does not return because it always throws.</returns>
    public sealed override IDisplayResult Edit(SubscriptionFlow model, BuildEditorContext context)
        => throw new NotImplementedException();

    /// <summary>
    /// Builds the asynchronous editor result for the handled subscription flow step.
    /// </summary>
    /// <param name="flow">The subscription flow being edited.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The editor result for the handled step.</returns>
    protected virtual Task<IDisplayResult> EditStepAsync(SubscriptionFlow flow, BuildEditorContext context)
    {
        return Task.FromResult(EditStep(flow, context));
    }

    /// <summary>
    /// Builds the synchronous editor result for the handled subscription flow step.
    /// </summary>
    /// <param name="flow">The subscription flow being edited.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The editor result for the handled step, or <see langword="null"/> when no editor shape is produced.</returns>
    protected virtual IDisplayResult EditStep(SubscriptionFlow flow, BuildEditorContext context)
    {
        return null;
    }

    /// <summary>
    /// Updates the model for the current subscription flow step.
    /// </summary>
    /// <param name="flow">The subscription flow being updated.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The editor result for the handled step, or <see langword="null"/> when another step is current.</returns>
    public sealed override Task<IDisplayResult> UpdateAsync(SubscriptionFlow flow, UpdateEditorContext context)
    {
        if (!flow.CurrentStepEquals(StepKey))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return UpdateStepAsync(flow, context);
    }

    /// <summary>
    /// Updates the model for the handled subscription flow step.
    /// </summary>
    /// <param name="flow">The subscription flow being updated.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The editor result for the handled step.</returns>
    protected virtual Task<IDisplayResult> UpdateStepAsync(SubscriptionFlow flow, UpdateEditorContext context)
    {
        return EditAsync(flow, context);
    }
}
