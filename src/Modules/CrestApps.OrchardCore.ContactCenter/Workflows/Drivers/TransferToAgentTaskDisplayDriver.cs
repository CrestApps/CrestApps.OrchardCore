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
/// Display driver for the <see cref="TransferToAgentTask"/> workflow activity.
/// </summary>
public sealed class TransferToAgentTaskDisplayDriver : ActivityDisplayDriver<TransferToAgentTask, TransferToAgentTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferToAgentTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager used to validate expressions.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public TransferToAgentTaskDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<TransferToAgentTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(TransferToAgentTask activity, TransferToAgentTaskViewModel model)
    {
        model.ActivityItemId = activity.ActivityItemId;
        model.QueueId = activity.QueueId;
        model.Reason = activity.Reason;
        model.Summary = activity.Summary;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(TransferToAgentTask activity, UpdateEditorContext context)
    {
        var model = new TransferToAgentTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.ActivityItemId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ActivityItemId), S["The Activity is required."]);
        }
        else if (!_liquidTemplateManager.Validate(model.ActivityItemId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ActivityItemId), S["The Activity expression is invalid."]);
        }

        // The queue, reason and summary are all optional: an empty queue means "use the queue the subject flow
        // already names", so only a syntactically broken expression is an error here.
        if (!string.IsNullOrWhiteSpace(model.QueueId) && !_liquidTemplateManager.Validate(model.QueueId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.QueueId), S["The Queue expression is invalid."]);
        }

        if (!string.IsNullOrWhiteSpace(model.Reason) && !_liquidTemplateManager.Validate(model.Reason, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Reason), S["The Reason expression is invalid."]);
        }

        if (!string.IsNullOrWhiteSpace(model.Summary) && !_liquidTemplateManager.Validate(model.Summary, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Summary), S["The Summary expression is invalid."]);
        }

        activity.ActivityItemId = model.ActivityItemId?.Trim();
        activity.QueueId = model.QueueId?.Trim();
        activity.Reason = model.Reason?.Trim();
        activity.Summary = model.Summary?.Trim();

        return Edit(activity, context);
    }
}
