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
/// Display driver for the <see cref="ScheduleCallbackTask"/> workflow activity.
/// </summary>
public sealed class ScheduleCallbackTaskDisplayDriver : ActivityDisplayDriver<ScheduleCallbackTask, ScheduleCallbackTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleCallbackTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager used to validate expressions.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public ScheduleCallbackTaskDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<ScheduleCallbackTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(ScheduleCallbackTask activity, ScheduleCallbackTaskViewModel model)
    {
        model.Destination = activity.Destination;
        model.DelayMinutes = activity.DelayMinutes;
        model.CampaignId = activity.CampaignId;
        model.QueueId = activity.QueueId;
        model.ContactContentItemId = activity.ContactContentItemId;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ScheduleCallbackTask activity, UpdateEditorContext context)
    {
        var model = new ScheduleCallbackTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Destination))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Destination), S["The Destination is required."]);
        }
        else if (!_liquidTemplateManager.Validate(model.Destination, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Destination), S["The Destination expression is invalid."]);
        }

        if (model.DelayMinutes < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DelayMinutes), S["The Delay cannot be negative."]);
        }

        ValidateOptional(context, model.CampaignId, nameof(model.CampaignId), S["The Campaign expression is invalid."]);
        ValidateOptional(context, model.QueueId, nameof(model.QueueId), S["The Queue expression is invalid."]);
        ValidateOptional(context, model.ContactContentItemId, nameof(model.ContactContentItemId), S["The Contact expression is invalid."]);

        activity.Destination = model.Destination?.Trim();
        activity.DelayMinutes = model.DelayMinutes;
        activity.CampaignId = model.CampaignId?.Trim();
        activity.QueueId = model.QueueId?.Trim();
        activity.ContactContentItemId = model.ContactContentItemId?.Trim();

        return Edit(activity, context);
    }

    private void ValidateOptional(UpdateEditorContext context, string expression, string field, LocalizedString message)
    {
        if (!string.IsNullOrWhiteSpace(expression) && !_liquidTemplateManager.Validate(expression, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, field, message);
        }
    }
}
