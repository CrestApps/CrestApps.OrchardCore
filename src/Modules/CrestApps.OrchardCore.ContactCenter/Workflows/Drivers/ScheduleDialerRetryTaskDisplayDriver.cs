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
/// Display driver for the <see cref="ScheduleDialerRetryTask"/> workflow activity.
/// </summary>
public sealed class ScheduleDialerRetryTaskDisplayDriver : ActivityDisplayDriver<ScheduleDialerRetryTask, ScheduleDialerRetryTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleDialerRetryTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager used to validate expressions.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public ScheduleDialerRetryTaskDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<ScheduleDialerRetryTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(ScheduleDialerRetryTask activity, ScheduleDialerRetryTaskViewModel model)
    {
        model.ActivityItemId = activity.ActivityItemId;
        model.DelayMinutes = activity.DelayMinutes;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ScheduleDialerRetryTask activity, UpdateEditorContext context)
    {
        var model = new ScheduleDialerRetryTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.ActivityItemId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ActivityItemId), S["The Activity is required."]);
        }
        else if (!_liquidTemplateManager.Validate(model.ActivityItemId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ActivityItemId), S["The Activity expression is invalid."]);
        }

        if (!string.IsNullOrWhiteSpace(model.DelayMinutes) && !_liquidTemplateManager.Validate(model.DelayMinutes, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DelayMinutes), S["The Delay expression is invalid."]);
        }

        activity.ActivityItemId = model.ActivityItemId?.Trim();
        activity.DelayMinutes = model.DelayMinutes?.Trim();

        return Edit(activity, context);
    }
}
