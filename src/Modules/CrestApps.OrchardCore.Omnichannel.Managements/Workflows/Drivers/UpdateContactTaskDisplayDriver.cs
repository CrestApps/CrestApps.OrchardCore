using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Drivers;

/// <summary>
/// Display driver for the <see cref="UpdateContactTask"/> workflow activity.
/// </summary>
public sealed class UpdateContactTaskDisplayDriver : ActivityDisplayDriver<UpdateContactTask, UpdateContactTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateContactTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager used to validate the expressions.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UpdateContactTaskDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<UpdateContactTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(UpdateContactTask activity, UpdateContactTaskViewModel model)
    {
        model.ContactContentItemId = activity.ContactContentItemId;
        model.ContentProperties = activity.ContentProperties;
        model.Publish = activity.Publish;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(UpdateContactTask activity, UpdateEditorContext context)
    {
        var model = new UpdateContactTaskViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.ContactContentItemId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ContactContentItemId), S["The contact is required."]);
        }
        else if (!_liquidTemplateManager.Validate(model.ContactContentItemId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ContactContentItemId), S["The contact expression is invalid."]);
        }

        if (string.IsNullOrWhiteSpace(model.ContentProperties))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ContentProperties), S["The fields to set are required."]);
        }
        else if (!_liquidTemplateManager.Validate(model.ContentProperties, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ContentProperties), S["The fields expression is invalid."]);
        }

        activity.ContactContentItemId = model.ContactContentItemId?.Trim();
        activity.ContentProperties = model.ContentProperties?.Trim();
        activity.Publish = model.Publish;

        return Edit(activity, context);
    }
}
