using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using CrestApps.OrchardCore.Sms.Workspace.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Sms.Workspace.Drivers;

/// <summary>
/// The display-management driver for an <see cref="SmsConversation"/> row in the inbox list. It resolves the
/// contact and assigned-agent display names once per row so the summary can surface them as badges. Other modules
/// can attach further badges (contact tags, CRM links) by adding shapes to the same display type.
/// </summary>
public sealed class SmsConversationDisplayDriver : DisplayDriver<SmsConversation>
{
    private readonly IContentManager _contentManager;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;

    public SmsConversationDisplayDriver(
        IContentManager contentManager,
        IAgentProfileManager agentProfileManager,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider)
    {
        _contentManager = contentManager;
        _agentProfileManager = agentProfileManager;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
    }

    public override IDisplayResult Display(SmsConversation conversation, BuildDisplayContext context)
    {
        return Initialize<SmsConversationRowViewModel>("SmsConversation_Fields_SummaryAdmin", async model =>
        {
            model.Conversation = conversation;
            model.ContactName = await ResolveContactNameAsync(conversation);
            model.AssignedToName = await ResolveAssignedToNameAsync(conversation);
        }).Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1");
    }

    private async Task<string> ResolveContactNameAsync(SmsConversation conversation)
    {
        if (string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            return null;
        }

        var contact = await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);

        return string.IsNullOrEmpty(contact?.DisplayText) ? null : contact.DisplayText;
    }

    private async Task<string> ResolveAssignedToNameAsync(SmsConversation conversation)
    {
        if (conversation.AssignmentStatus != SmsConversationAssignmentStatus.Assigned ||
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
