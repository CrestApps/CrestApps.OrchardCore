using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;

/// <summary>
/// Display driver for the <see cref="EnqueueActivityTask"/> workflow activity.
/// </summary>
public sealed class EnqueueActivityTaskDisplayDriver : ActivityDisplayDriver<EnqueueActivityTask, EnqueueActivityTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnqueueActivityTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager used to validate expressions.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public EnqueueActivityTaskDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<EnqueueActivityTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(EnqueueActivityTask activity, EnqueueActivityTaskViewModel model)
    {
        model.ActivityItemId = activity.ActivityItemId;
        model.QueueId = activity.QueueId;
        model.Priority = activity.Priority;
        model.Priorities = GetPriorities();
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(EnqueueActivityTask activity, UpdateEditorContext context)
    {
        var model = new EnqueueActivityTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.ActivityItemId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ActivityItemId), S["The Activity is required."]);
        }
        else if (!_liquidTemplateManager.Validate(model.ActivityItemId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ActivityItemId), S["The Activity expression is invalid."]);
        }

        if (string.IsNullOrWhiteSpace(model.QueueId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.QueueId), S["The Queue is required."]);
        }
        else if (!_liquidTemplateManager.Validate(model.QueueId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.QueueId), S["The Queue expression is invalid."]);
        }

        activity.ActivityItemId = model.ActivityItemId?.Trim();
        activity.QueueId = model.QueueId?.Trim();
        activity.Priority = model.Priority;

        return Edit(activity, context);
    }

    private IEnumerable<SelectListItem> GetPriorities()
    {
        return
        [
            new SelectListItem(S["Use queue default"].Value, string.Empty),
            new SelectListItem(S["Lowest"].Value, nameof(InteractionPriority.Lowest)),
            new SelectListItem(S["Low"].Value, nameof(InteractionPriority.Low)),
            new SelectListItem(S["Normal"].Value, nameof(InteractionPriority.Normal)),
            new SelectListItem(S["High"].Value, nameof(InteractionPriority.High)),
            new SelectListItem(S["Highest"].Value, nameof(InteractionPriority.Highest)),
        ];
    }
}
