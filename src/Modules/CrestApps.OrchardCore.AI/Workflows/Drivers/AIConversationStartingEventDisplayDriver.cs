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
/// Edits the AI Conversation Starting event's channel and profile filters.
/// </summary>
public sealed class AIConversationStartingEventDisplayDriver : ActivityDisplayDriver<AIConversationStartingEvent, AIConversationStartingEventViewModel>
{
    private readonly IAIProfileStore _profileStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationStartingEventDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The AI profile store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIConversationStartingEventDisplayDriver(IAIProfileStore profileStore, IStringLocalizer<AIConversationStartingEventDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(AIConversationStartingEvent activity, AIConversationStartingEventViewModel model)
    {
        model.Channel = activity.Channel;
        model.ProfileId = activity.ProfileId;
        model.Channels = AIConversationChannelOptions.Get(S);
        model.Profiles = (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
            .Select(profile => new SelectListItem(profile.DisplayText, profile.ItemId));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIConversationStartingEvent activity, UpdateEditorContext context)
    {
        var model = new AIConversationStartingEventViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        activity.Channel = model.Channel;
        activity.ProfileId = model.ProfileId;

        return Edit(activity, context);
    }
}
