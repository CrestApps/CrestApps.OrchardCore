using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Drivers;

/// <summary>
/// The display-management driver for a customer row in the workspace list (their most recent conversation). It resolves the
/// contact and assigned-agent display names once per row so the summary can surface them as badges. Other modules
/// can attach further badges (contact tags, CRM links) by adding shapes to the same display type.
/// </summary>
public sealed class MessagingConversationDisplayDriver : DisplayDriver<MessagingConversation>
{
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IContentManager _contentManager;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;

    public MessagingConversationDisplayDriver(
        IMessagingChannelResolver channelResolver,
        IContentManager contentManager,
        IAgentProfileManager agentProfileManager,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider)
    {
        _channelResolver = channelResolver;
        _contentManager = contentManager;
        _agentProfileManager = agentProfileManager;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
    }

    public override IDisplayResult Display(MessagingConversation conversation, BuildDisplayContext context)
    {
        return Initialize<ConversationRowViewModel>("MessagingConversation_Fields_SummaryAdmin", async model =>
        {
            var channel = _channelResolver.Get(conversation.Channel);

            model.Conversation = conversation;
            model.ContactName = await ResolveContactNameAsync(conversation);
            model.ContactAddressDisplay = channel?.FormatAddress(conversation.ContactAddress) ?? conversation.ContactAddress;
            model.AssignedToName = await ResolveAssignedToNameAsync(conversation);
            model.Channel = channel is null
                ? null
                : new ChannelViewModel
                {
                    Name = channel.Name,
                    DisplayName = channel.DisplayName.Value,
                    IconCssClass = channel.IconCssClass,
                };
        }).Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1");
    }

    private async Task<string> ResolveContactNameAsync(MessagingConversation conversation)
    {
        if (string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            return null;
        }

        var contact = await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);

        return string.IsNullOrEmpty(contact?.DisplayText) ? null : contact.DisplayText;
    }

    private async Task<string> ResolveAssignedToNameAsync(MessagingConversation conversation)
    {
        if (conversation.AssignmentStatus != ConversationAssignmentStatus.Assigned ||
            string.IsNullOrEmpty(conversation.AssignedAgentId))
        {
            return null;
        }

        var agent = await _agentProfileManager.FindByIdAsync(conversation.AssignedAgentId);

        if (agent is null)
        {
            return null;
        }

        // Prefer the user's real full name resolved through IDisplayNameProvider (first/last name, etc.) so the
        // inbox never surfaces an opaque user id or a bare user name. Fall back to the agent profile's own labels
        // only when the underlying user cannot be resolved or has no display name configured.
        if (!string.IsNullOrEmpty(agent.UserId))
        {
            var user = await _userManager.FindByIdAsync(agent.UserId);

            if (user is not null)
            {
                var displayName = await _displayNameProvider.GetAsync(user);

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }
            }
        }

        return !string.IsNullOrEmpty(agent.DisplayName)
            ? agent.DisplayName
            : !string.IsNullOrEmpty(agent.UserName) ? agent.UserName : agent.Name;
    }
}
