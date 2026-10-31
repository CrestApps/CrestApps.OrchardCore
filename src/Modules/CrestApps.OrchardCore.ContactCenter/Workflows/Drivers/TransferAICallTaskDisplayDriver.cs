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
/// Edits the Transfer AI Call workflow task.
/// </summary>
public sealed class TransferAICallTaskDisplayDriver : ActivityDisplayDriver<TransferAICallTask, TransferAICallTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferAICallTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager, which validates the expressions.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransferAICallTaskDisplayDriver(ILiquidTemplateManager liquidTemplateManager, IStringLocalizer<TransferAICallTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(TransferAICallTask activity, TransferAICallTaskViewModel model)
    {
        model.ActivityId = activity.ActivityId;
        model.PhoneNumber = activity.PhoneNumber;
        model.Mode = activity.Mode;
        model.Briefing = activity.Briefing;
        model.Introduction = activity.Introduction;
        model.RingTimeoutSeconds = activity.RingTimeoutSeconds;
        model.HoldAudio = activity.HoldAudio;
        model.ShowCallerNumber = activity.ShowCallerNumber;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(TransferAICallTask activity, UpdateEditorContext context)
    {
        var model = new TransferAICallTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.ActivityId) || !_liquidTemplateManager.Validate(model.ActivityId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ActivityId), S["A valid Liquid expression for the call's activity is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.PhoneNumber) || !_liquidTemplateManager.Validate(model.PhoneNumber, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PhoneNumber), S["A valid phone number or Liquid expression is required."]);
        }

        foreach (var (name, value) in new[] { (nameof(model.Briefing), model.Briefing), (nameof(model.Introduction), model.Introduction), (nameof(model.HoldAudio), model.HoldAudio) })
        {
            if (!string.IsNullOrWhiteSpace(value) && !_liquidTemplateManager.Validate(value, out _))
            {
                context.Updater.ModelState.AddModelError(Prefix, name, S["This is not a valid Liquid template."]);
            }
        }

        if (model.RingTimeoutSeconds is < 5 or > 120)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RingTimeoutSeconds), S["The ring time must be between 5 and 120 seconds."]);
        }

        activity.ActivityId = model.ActivityId?.Trim();
        activity.PhoneNumber = model.PhoneNumber?.Trim();
        activity.Mode = model.Mode;
        activity.Briefing = model.Briefing;
        activity.Introduction = model.Introduction;
        activity.RingTimeoutSeconds = model.RingTimeoutSeconds;
        activity.HoldAudio = model.HoldAudio?.Trim();
        activity.ShowCallerNumber = model.ShowCallerNumber;

        return Edit(activity, context);
    }
}
