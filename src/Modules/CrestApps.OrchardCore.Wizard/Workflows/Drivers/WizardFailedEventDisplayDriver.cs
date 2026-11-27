using CrestApps.OrchardCore.Wizard.Workflows.Models;
using CrestApps.OrchardCore.Wizard.Workflows.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Wizard.Workflows.Drivers;

/// <summary>
/// Display driver for the <see cref="WizardFailedEvent"/> activity.
/// </summary>
public sealed class WizardFailedEventDisplayDriver : ActivityDisplayDriver<WizardFailedEvent, WizardEventViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WizardFailedEventDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WizardFailedEventDisplayDriver(IStringLocalizer<WizardFailedEventDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(WizardFailedEvent activity, BuildEditorContext context)
    {
        return Initialize<WizardEventViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Settings"], "10"));
    }

    /// <inheritdoc/>
    protected override void EditActivity(WizardFailedEvent activity, WizardEventViewModel model)
    {
        model.WizardType = activity.WizardType;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(WizardFailedEvent activity, UpdateEditorContext context)
    {
        var model = new WizardEventViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        activity.WizardType = model.WizardType;

        return Edit(activity, context);
    }
}
