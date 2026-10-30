using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Displays the AI Conversation Flow Started workflow event.
/// </summary>
public sealed class AIConversationFlowStartedEventDisplayDriver : ActivityDisplayDriver<AIConversationFlowStartedEvent>
{
}

/// <summary>
/// Edits the AI Conversation Stage task.
/// </summary>
public sealed class AIConversationStageTaskDisplayDriver : ActivityDisplayDriver<AIConversationStageTask, AIConversationStageTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationStageTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">Validates the instructions.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIConversationStageTaskDisplayDriver(ILiquidTemplateManager liquidTemplateManager, IStringLocalizer<AIConversationStageTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(AIConversationStageTask activity, AIConversationStageTaskViewModel model)
    {
        model.StageName = activity.StageName;
        model.Instructions = activity.Instructions;
        model.Outcomes = activity.WaysOut;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIConversationStageTask activity, UpdateEditorContext context)
    {
        var model = new AIConversationStageTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.StageName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.StageName), S["The stage needs a name."]);
        }

        if (!string.IsNullOrWhiteSpace(model.Instructions) && !_liquidTemplateManager.Validate(model.Instructions, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Instructions), S["The instructions are not a valid Liquid template."]);
        }

        activity.StageName = model.StageName?.Trim();
        activity.Instructions = model.Instructions;
        activity.WaysOut = string.Join('\n', AIConversationFlowPrompt.ParseOutcomes(model.Outcomes).Select(outcome => string.IsNullOrEmpty(outcome.When) ? outcome.Name : $"{outcome.Name}: {outcome.When}"));

        return Edit(activity, context);
    }
}
