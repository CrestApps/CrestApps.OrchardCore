using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Edits the Evaluate AI Conversation task.
/// </summary>
public sealed class EvaluateConversationTaskDisplayDriver : ActivityDisplayDriver<EvaluateConversationTask, EvaluateConversationTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluateConversationTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager, which validates the templates.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EvaluateConversationTaskDisplayDriver(ILiquidTemplateManager liquidTemplateManager, IStringLocalizer<EvaluateConversationTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(EvaluateConversationTask activity, EvaluateConversationTaskViewModel model)
    {

        model.SessionId = activity.SessionId;
        model.Criteria = activity.Criteria;
        model.PassingScore = activity.PassingScore;
        model.Label = activity.Label;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(EvaluateConversationTask activity, UpdateEditorContext context)
    {
        var model = new EvaluateConversationTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);


        if (string.IsNullOrWhiteSpace(model.SessionId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SessionId), S["Say which conversation to evaluate."]);
        }

        if (model.PassingScore is < 0 or > 100)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PassingScore), S["The passing score is from 0 to 100."]);
        }

        if (!string.IsNullOrWhiteSpace(model.Criteria) && !_liquidTemplateManager.Validate(model.Criteria, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Criteria), S["The criteria is not a valid Liquid template."]);
        }
        if (!string.IsNullOrWhiteSpace(model.SessionId) && !_liquidTemplateManager.Validate(model.SessionId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SessionId), S["The conversation is not a valid Liquid template."]);
        }

        activity.SessionId = model.SessionId?.Trim();
        activity.Criteria = model.Criteria;
        activity.PassingScore = model.PassingScore;
        activity.Label = model.Label?.Trim();

        return Edit(activity, context);
    }
}
