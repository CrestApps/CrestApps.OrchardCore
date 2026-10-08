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
/// Display driver for the <see cref="SetAgentPresenceTask"/> workflow activity.
/// </summary>
public sealed class SetAgentPresenceTaskDisplayDriver : ActivityDisplayDriver<SetAgentPresenceTask, SetAgentPresenceTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetAgentPresenceTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager used to validate expressions.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public SetAgentPresenceTaskDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<SetAgentPresenceTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(SetAgentPresenceTask activity, SetAgentPresenceTaskViewModel model)
    {
        model.UserId = activity.UserId;
        model.Status = activity.Status;
        model.Reason = activity.Reason;
        model.Statuses = GetStatuses();
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(SetAgentPresenceTask activity, UpdateEditorContext context)
    {
        var model = new SetAgentPresenceTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.UserId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserId), S["The User is required."]);
        }
        else if (!_liquidTemplateManager.Validate(model.UserId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserId), S["The User expression is invalid."]);
        }

        if (!string.IsNullOrWhiteSpace(model.Reason) && !_liquidTemplateManager.Validate(model.Reason, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Reason), S["The Reason expression is invalid."]);
        }

        activity.UserId = model.UserId?.Trim();
        activity.Status = model.Status;
        activity.Reason = model.Reason?.Trim();

        return Edit(activity, context);
    }

    private IEnumerable<SelectListItem> GetStatuses()
    {
        // Reservation- and work-lifecycle-owned states (Reserved, Busy, WrapUp) are deliberately excluded:
        // they are applied by the contact center runtime as a side effect of an offer, an active interaction,
        // or post-interaction wrap-up. Letting automation set them directly would create a parked profile with
        // no backing reservation or call and block future routing. The task also rejects them at execution so
        // an imported workflow definition cannot bypass this picker.
        return
        [
            new SelectListItem(S["Offline"].Value, nameof(AgentPresenceStatus.Offline)),
            new SelectListItem(S["Available"].Value, nameof(AgentPresenceStatus.Available)),
            new SelectListItem(S["Break"].Value, nameof(AgentPresenceStatus.Break)),
            new SelectListItem(S["Requested break"].Value, nameof(AgentPresenceStatus.RequestBreak)),
            new SelectListItem(S["Away"].Value, nameof(AgentPresenceStatus.Away)),
            new SelectListItem(S["Do not disturb"].Value, nameof(AgentPresenceStatus.DoNotDisturb)),
            new SelectListItem(S["Meeting"].Value, nameof(AgentPresenceStatus.Meeting)),
            new SelectListItem(S["Training"].Value, nameof(AgentPresenceStatus.Training)),
            new SelectListItem(S["After-hours unavailable"].Value, nameof(AgentPresenceStatus.AfterHoursUnavailable)),
        ];
    }
}
