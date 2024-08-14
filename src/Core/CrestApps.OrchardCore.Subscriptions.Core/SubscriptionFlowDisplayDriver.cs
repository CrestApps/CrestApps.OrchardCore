using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Core;

public abstract class SubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    protected abstract string StepKey { get; }

    public sealed override Task<IDisplayResult> DisplayAsync(SubscriptionFlow flow, BuildDisplayContext context)
    {
        if (!flow.CurrentStepEquals(StepKey))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return DisplayStepAsync(flow, context);
    }

    public sealed override IDisplayResult Display(SubscriptionFlow model, BuildDisplayContext context)
        => throw new NotImplementedException();

    protected virtual Task<IDisplayResult> DisplayStepAsync(SubscriptionFlow flow, BuildDisplayContext context)
    {
        return Task.FromResult(DisplayStep(flow, context));
    }

    protected virtual IDisplayResult DisplayStep(SubscriptionFlow flow, BuildDisplayContext context)
    {
        return null;
    }

    public sealed override Task<IDisplayResult> EditAsync(SubscriptionFlow flow, BuildEditorContext context)
    {
        if (!flow.CurrentStepEquals(StepKey))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return EditStepAsync(flow, context);
    }

    public sealed override IDisplayResult Edit(SubscriptionFlow model, BuildEditorContext context)
        => throw new NotImplementedException();

    protected virtual Task<IDisplayResult> EditStepAsync(SubscriptionFlow flow, BuildEditorContext context)
    {
        return Task.FromResult(EditStep(flow, context));
    }

    protected virtual IDisplayResult EditStep(SubscriptionFlow flow, BuildEditorContext context)
    {
        return null;
    }

    public sealed override Task<IDisplayResult> UpdateAsync(SubscriptionFlow flow, UpdateEditorContext context)
    {
        if (!flow.CurrentStepEquals(StepKey))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return UpdateStepAsync(flow, context);
    }

    protected virtual Task<IDisplayResult> UpdateStepAsync(SubscriptionFlow flow, UpdateEditorContext context)
    {
        return EditAsync(flow, context);
    }
}
