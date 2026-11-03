using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Edits the Update Contact Memory task.
/// </summary>
public sealed class UpdateContactMemoryTaskDisplayDriver : ActivityDisplayDriver<UpdateContactMemoryTask, UpdateContactMemoryTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateContactMemoryTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager, which validates the templates.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UpdateContactMemoryTaskDisplayDriver(ILiquidTemplateManager liquidTemplateManager, IStringLocalizer<UpdateContactMemoryTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(UpdateContactMemoryTask activity, UpdateContactMemoryTaskViewModel model)
    {
        model.SessionId = activity.SessionId;
        model.ContactId = activity.ContactId;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(UpdateContactMemoryTask activity, UpdateEditorContext context)
    {
        var model = new UpdateContactMemoryTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.SessionId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SessionId), S["Say which conversation to remember."]);
        }
        else if (!_liquidTemplateManager.Validate(model.SessionId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SessionId), S["The conversation is not a valid Liquid template."]);
        }

        if (!string.IsNullOrWhiteSpace(model.ContactId) && !_liquidTemplateManager.Validate(model.ContactId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ContactId), S["The contact is not a valid Liquid template."]);
        }

        activity.SessionId = model.SessionId?.Trim();
        activity.ContactId = model.ContactId?.Trim();

        return Edit(activity, context);
    }
}
