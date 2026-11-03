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
/// The channel choices of the AI conversation events.
/// </summary>
internal static class AIConversationChannelOptions
{
    /// <summary>
    /// Gets the channels to choose from.
    /// </summary>
    /// <param name="S">The string localizer.</param>
    /// <returns>The options.</returns>
    public static IEnumerable<SelectListItem> Get(IStringLocalizer S)
        =>
        [
            new(S["Phone calls"], AIConversationChannels.Phone),
            new(S["Text messages"], AIConversationChannels.Sms),
            new(S["Chat"], AIConversationChannels.Chat),
            new(S["Email"], AIConversationChannels.Email),
        ];
}
