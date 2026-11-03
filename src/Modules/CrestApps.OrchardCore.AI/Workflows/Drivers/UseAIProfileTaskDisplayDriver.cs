using CrestApps.Core;
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
/// Edits the Use AI Profile task.
/// </summary>
public sealed class UseAIProfileTaskDisplayDriver : ActivityDisplayDriver<UseAIProfileTask, UseAIProfileTaskViewModel>
{
    private readonly IAIProfileStore _profileStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UseAIProfileTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The AI profile store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UseAIProfileTaskDisplayDriver(IAIProfileStore profileStore, IStringLocalizer<UseAIProfileTaskDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(UseAIProfileTask activity, UseAIProfileTaskViewModel model)
    {
        model.ProfileId = activity.ProfileId;
        model.ProfileVersion = activity.ProfileVersion;
        model.Profiles = (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
            .Where(profile => !profile.Has<AIProfileVersionCopy>())
            .Select(profile => new SelectListItem(profile.DisplayText, profile.ItemId));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(UseAIProfileTask activity, UpdateEditorContext context)
    {
        var model = new UseAIProfileTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.ProfileId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileId), S["Choose the AI profile to answer with."]);
        }

        if (model.ProfileVersion is <= 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileVersion), S["The version must be a positive number, or empty for the current one."]);
        }

        activity.ProfileId = model.ProfileId;
        activity.ProfileVersion = model.ProfileVersion;

        return Edit(activity, context);
    }
}
