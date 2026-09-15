using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;

/// <summary>
/// Display driver for the <see cref="StartOmnichannelActivityTask"/> workflow activity.
/// </summary>
public sealed class StartOmnichannelActivityTaskDisplayDriver : ActivityDisplayDriver<StartOmnichannelActivityTask, StartOmnichannelActivityTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartOmnichannelActivityTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager used to validate expressions.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public StartOmnichannelActivityTaskDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<StartOmnichannelActivityTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(StartOmnichannelActivityTask activity, StartOmnichannelActivityTaskViewModel model)
    {
        model.ActivityItemId = activity.ActivityItemId;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(StartOmnichannelActivityTask activity, UpdateEditorContext context)
    {
        var model = new StartOmnichannelActivityTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.ActivityItemId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ActivityItemId), S["The Activity is required."]);
        }
        else if (!_liquidTemplateManager.Validate(model.ActivityItemId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ActivityItemId), S["The Activity expression is invalid."]);
        }

        activity.ActivityItemId = model.ActivityItemId?.Trim();

        return Edit(activity, context);
    }
}
