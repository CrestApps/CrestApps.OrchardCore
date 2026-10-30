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
/// Edits the AI Call Action workflow task.
/// </summary>
public sealed class AICallActionTaskDisplayDriver : ActivityDisplayDriver<AICallActionTask, AICallActionTaskViewModel>
{
    private static readonly HashSet<string> _actions = new(StringComparer.Ordinal)
    {
        AICallActions.Message,
        AICallActions.TakeOver,
        AICallActions.TransferToNumber,
        AICallActions.SwitchProfile,
        AICallActions.End,
    };

    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICallActionTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager, which validates the expressions.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AICallActionTaskDisplayDriver(ILiquidTemplateManager liquidTemplateManager, IStringLocalizer<AICallActionTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(AICallActionTask activity, AICallActionTaskViewModel model)
    {
        model.ActivityId = activity.ActivityId;
        model.Action = activity.Action;
        model.Value = activity.Value;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AICallActionTask activity, UpdateEditorContext context)
    {
        var model = new AICallActionTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.ActivityId) || !_liquidTemplateManager.Validate(model.ActivityId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ActivityId), S["A valid Liquid expression for the call's activity is required."]);
        }

        if (!_actions.Contains(model.Action ?? string.Empty))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Action), S["Choose what to do."]);
        }
        else if (model.Action != AICallActions.End && string.IsNullOrWhiteSpace(model.Value))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Value), S["This action needs a value."]);
        }

        if (!string.IsNullOrWhiteSpace(model.Value) && !_liquidTemplateManager.Validate(model.Value, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Value), S["The value is not a valid Liquid template."]);
        }

        activity.ActivityId = model.ActivityId?.Trim();
        activity.Action = model.Action;
        activity.Value = model.Value;

        return Edit(activity, context);
    }
}
