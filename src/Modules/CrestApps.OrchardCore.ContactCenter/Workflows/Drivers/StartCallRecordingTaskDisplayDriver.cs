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
/// Display driver for the <see cref="StartCallRecordingTask"/> workflow activity.
/// </summary>
public sealed class StartCallRecordingTaskDisplayDriver : ActivityDisplayDriver<StartCallRecordingTask, RecordingInteractionTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartCallRecordingTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager used to validate expressions.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public StartCallRecordingTaskDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<StartCallRecordingTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(StartCallRecordingTask activity, RecordingInteractionTaskViewModel model)
    {
        model.InteractionId = activity.InteractionId;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(StartCallRecordingTask activity, UpdateEditorContext context)
    {
        var model = new RecordingInteractionTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.InteractionId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.InteractionId), S["The Interaction is required."]);
        }
        else if (!_liquidTemplateManager.Validate(model.InteractionId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.InteractionId), S["The Interaction expression is invalid."]);
        }

        activity.InteractionId = model.InteractionId?.Trim();

        return Edit(activity, context);
    }
}
