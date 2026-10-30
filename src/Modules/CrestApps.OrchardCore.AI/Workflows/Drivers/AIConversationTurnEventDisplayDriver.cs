using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Edits the AI Conversation Turn event's channel, profile and role filters.
/// </summary>
public sealed class AIConversationTurnEventDisplayDriver : ActivityDisplayDriver<AIConversationTurnEvent, AIConversationTurnEventViewModel>
{
    private readonly IAIProfileStore _profileStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationTurnEventDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The AI profile store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIConversationTurnEventDisplayDriver(IAIProfileStore profileStore, IStringLocalizer<AIConversationTurnEventDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(AIConversationTurnEvent activity, AIConversationTurnEventViewModel model)
    {
        model.Channel = activity.Channel;
        model.ProfileId = activity.ProfileId;
        model.Role = activity.Role;
        model.Channels = AIConversationChannelOptions.Get(S);
        model.Roles =
        [
            new SelectListItem(S["What the customer says"], AIConversationTurn.CustomerRole),
            new SelectListItem(S["What the AI says"], AIConversationTurn.AssistantRole),
        ];
        model.Profiles = (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
            .Select(profile => new SelectListItem(profile.DisplayText, profile.ItemId));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIConversationTurnEvent activity, UpdateEditorContext context)
    {
        var model = new AIConversationTurnEventViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        activity.Channel = model.Channel;
        activity.ProfileId = model.ProfileId;
        activity.Role = model.Role;

        return Edit(activity, context);
    }
}
