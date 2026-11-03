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
/// Edits the AI Conversation Evaluated event.
/// </summary>
public sealed class AIConversationEvaluatedEventDisplayDriver : ActivityDisplayDriver<AIConversationEvaluatedEvent, AIConversationEvaluatedEventViewModel>
{
    private readonly IAIProfileStore _profileStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationEvaluatedEventDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The AI profile store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIConversationEvaluatedEventDisplayDriver(IAIProfileStore profileStore, IStringLocalizer<AIConversationEvaluatedEventDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(AIConversationEvaluatedEvent activity, AIConversationEvaluatedEventViewModel model)
    {

        model.ProfileId = activity.ProfileId;
        model.OnlyFailures = activity.OnlyFailures;
        model.Profiles = await GetProfilesAsync();
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIConversationEvaluatedEvent activity, UpdateEditorContext context)
    {
        var model = new AIConversationEvaluatedEventViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);


        activity.ProfileId = model.ProfileId;
        activity.OnlyFailures = model.OnlyFailures;

        return Edit(activity, context);
    }

    private async Task<IEnumerable<SelectListItem>> GetProfilesAsync()
        => (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
            .Select(profile => new SelectListItem(profile.DisplayText, profile.ItemId));
}
