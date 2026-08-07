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
/// Display driver for the <see cref="StopCallRecordingTask"/> workflow activity.
/// </summary>
public sealed class StopCallRecordingTaskDisplayDriver : ActivityDisplayDriver<StopCallRecordingTask, RecordingInteractionTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StopCallRecordingTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager used to validate expressions.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public StopCallRecordingTaskDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<StopCallRecordingTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(StopCallRecordingTask activity, RecordingInteractionTaskViewModel model)
    {
        model.InteractionId = activity.InteractionId;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(StopCallRecordingTask activity, UpdateEditorContext context)
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
