using CrestApps.OrchardCore.Wizard.Workflows.Models;
using CrestApps.OrchardCore.Wizard.Workflows.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Wizard.Workflows.Drivers;

/// <summary>
/// Display driver for the <see cref="WizardStepDisplayedEvent"/> activity.
/// </summary>
public sealed class WizardStepDisplayedEventDisplayDriver : ActivityDisplayDriver<WizardStepDisplayedEvent, WizardEventViewModel>
{
    /// <inheritdoc/>
    protected override void EditActivity(WizardStepDisplayedEvent activity, WizardEventViewModel model)
    {
        model.WizardType = activity.WizardType;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(WizardStepDisplayedEvent activity, UpdateEditorContext context)
    {
        var model = new WizardEventViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        activity.WizardType = model.WizardType;

        return Edit(activity, context);
    }
}
