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
/// Edits the Decline Conversation task.
/// </summary>
public sealed class DeclineConversationTaskDisplayDriver : ActivityDisplayDriver<DeclineConversationTask, DeclineConversationTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeclineConversationTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager, which validates the reason.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DeclineConversationTaskDisplayDriver(ILiquidTemplateManager liquidTemplateManager, IStringLocalizer<DeclineConversationTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(DeclineConversationTask activity, DeclineConversationTaskViewModel model)
        => model.Reason = activity.Reason;

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(DeclineConversationTask activity, UpdateEditorContext context)
    {
        var model = new DeclineConversationTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!string.IsNullOrWhiteSpace(model.Reason) && !_liquidTemplateManager.Validate(model.Reason, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Reason), S["The reason is not a valid Liquid template."]);
        }

        activity.Reason = model.Reason;

        return Edit(activity, context);
    }
}
