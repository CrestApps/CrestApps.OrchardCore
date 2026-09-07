using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Checkout.Core;

/// <summary>
/// A display driver base for a single checkout step. It renders only while its own step is the current one,
/// so every step can be authored as an independent driver without each having to re-check which step the
/// customer is on — and so a step can never accidentally render its editor (or worse, process its post) while
/// the customer is somewhere else in the flow.
/// </summary>
public abstract class CheckoutFlowDisplayDriver : DisplayDriver<CheckoutFlow>
{
    /// <summary>
    /// Gets the step key this driver renders.
    /// </summary>
    protected abstract string StepKey { get; }

    /// <inheritdoc/>
    public sealed override Task<IDisplayResult> DisplayAsync(CheckoutFlow flow, BuildDisplayContext context)
    {
        if (!flow.CurrentStepEquals(StepKey))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return DisplayStepAsync(flow, context);
    }

    /// <inheritdoc/>
    public sealed override IDisplayResult Display(CheckoutFlow model, BuildDisplayContext context)
        => throw new NotSupportedException("Checkout step drivers are asynchronous.");

    /// <summary>
    /// Builds the display shape for the handled step.
    /// </summary>
    /// <param name="flow">The checkout flow.</param>
    /// <param name="context">The display context.</param>
    protected virtual Task<IDisplayResult> DisplayStepAsync(CheckoutFlow flow, BuildDisplayContext context)
        => Task.FromResult(DisplayStep(flow, context));

    /// <summary>
    /// Builds the display shape for the handled step.
    /// </summary>
    /// <param name="flow">The checkout flow.</param>
    /// <param name="context">The display context.</param>
    protected virtual IDisplayResult DisplayStep(CheckoutFlow flow, BuildDisplayContext context)
        => null;

    /// <inheritdoc/>
    public sealed override Task<IDisplayResult> EditAsync(CheckoutFlow flow, BuildEditorContext context)
    {
        if (!flow.CurrentStepEquals(StepKey))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return EditStepAsync(flow, context);
    }

    /// <inheritdoc/>
    public sealed override IDisplayResult Edit(CheckoutFlow model, BuildEditorContext context)
        => throw new NotSupportedException("Checkout step drivers are asynchronous.");

    /// <summary>
    /// Builds the editor shape for the handled step.
    /// </summary>
    /// <param name="flow">The checkout flow.</param>
    /// <param name="context">The editor context.</param>
    protected virtual Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
        => Task.FromResult(EditStep(flow, context));

    /// <summary>
    /// Builds the editor shape for the handled step.
    /// </summary>
    /// <param name="flow">The checkout flow.</param>
    /// <param name="context">The editor context.</param>
    protected virtual IDisplayResult EditStep(CheckoutFlow flow, BuildEditorContext context)
        => null;

    /// <inheritdoc/>
    public sealed override Task<IDisplayResult> UpdateAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        if (!flow.CurrentStepEquals(StepKey))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return UpdateStepAsync(flow, context);
    }

    /// <summary>
    /// Applies a posted step and rebuilds its editor.
    /// </summary>
    /// <param name="flow">The checkout flow.</param>
    /// <param name="context">The update context.</param>
    protected virtual Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
        => EditStepAsync(flow, context);
}
