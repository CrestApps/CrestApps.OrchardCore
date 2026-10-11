using CrestApps.OrchardCore.Wizard.Workflows.Models;
using CrestApps.OrchardCore.Wizard.Workflows.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Wizard.Workflows.Drivers;

/// <summary>
/// Display driver for the <see cref="WizardStartedEvent"/> activity.
/// </summary>
public sealed class WizardStartedEventDisplayDriver : ActivityDisplayDriver<WizardStartedEvent, WizardEventViewModel>
{
    /// <inheritdoc/>
    protected override void EditActivity(WizardStartedEvent activity, WizardEventViewModel model)
    {
        model.WizardType = activity.WizardType;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(WizardStartedEvent activity, UpdateEditorContext context)
    {
        var model = new WizardEventViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        activity.WizardType = model.WizardType;

        return Edit(activity, context);
    }
}
