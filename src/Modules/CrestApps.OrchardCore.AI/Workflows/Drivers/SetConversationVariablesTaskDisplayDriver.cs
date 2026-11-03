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
/// Edits the Set Conversation Variables task.
/// </summary>
public sealed class SetConversationVariablesTaskDisplayDriver : ActivityDisplayDriver<SetConversationVariablesTask, SetConversationVariablesTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetConversationVariablesTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager, which validates the template.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SetConversationVariablesTaskDisplayDriver(ILiquidTemplateManager liquidTemplateManager, IStringLocalizer<SetConversationVariablesTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(SetConversationVariablesTask activity, SetConversationVariablesTaskViewModel model)
        => model.Variables = activity.Variables;

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(SetConversationVariablesTask activity, UpdateEditorContext context)
    {
        var model = new SetConversationVariablesTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Variables))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Variables), S["Add at least one variable."]);
        }
        else if (!_liquidTemplateManager.Validate(model.Variables, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Variables), S["The variables are not a valid Liquid template."]);
        }

        activity.Variables = model.Variables;

        return Edit(activity, context);
    }
}
