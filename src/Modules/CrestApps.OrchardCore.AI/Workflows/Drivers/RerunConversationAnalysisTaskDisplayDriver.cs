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
/// Edits the Rerun AI Conversation Analysis task.
/// </summary>
public sealed class RerunConversationAnalysisTaskDisplayDriver : ActivityDisplayDriver<RerunConversationAnalysisTask, RerunConversationAnalysisTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RerunConversationAnalysisTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager, which validates the templates.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RerunConversationAnalysisTaskDisplayDriver(ILiquidTemplateManager liquidTemplateManager, IStringLocalizer<RerunConversationAnalysisTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(RerunConversationAnalysisTask activity, RerunConversationAnalysisTaskViewModel model)
    {

        model.SessionId = activity.SessionId;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(RerunConversationAnalysisTask activity, UpdateEditorContext context)
    {
        var model = new RerunConversationAnalysisTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);


        if (string.IsNullOrWhiteSpace(model.SessionId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SessionId), S["Say which conversation to analyze again."]);
        }
        else if (!_liquidTemplateManager.Validate(model.SessionId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SessionId), S["The conversation is not a valid Liquid template."]);
        }

        activity.SessionId = model.SessionId?.Trim();

        return Edit(activity, context);
    }
}
