using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core;

/// <summary>
/// The permissions exposed by the Omnichannel Messaging workspace. They apply to every messaging channel, so a
/// person allowed to use the workspace can work a conversation whatever channel it runs on. Queue membership
/// itself is governed by the existing Contact Center agent entitlements, so there is no parallel membership
/// permission here.
/// </summary>
/// <remarks>
/// A conversation is authorized the way Orchard Core authorizes a content item: the caller asks for
/// <see cref="ViewAllConversations"/> with the conversation as the resource, a supervisor holding it is granted
/// outright, and the messaging conversation authorization handler grants anybody else through the narrower
/// <see cref="ViewQueueConversations"/> or <see cref="ViewOwnConversations"/> when the conversation is theirs to work.
/// The list passed to each permission names the permissions that imply it.
/// </remarks>
public static class MessagingPermissions
{
    /// <summary>
    /// Grants management of the workspace's shared configuration: canned-response templates and the inbound
    /// routing configured on each messaging endpoint.
    /// </summary>
    public static readonly Permission ManageMessaging = new("ManageMessaging", LocalizationSource.Create("Manage the messaging workspace", typeof(MessagingPermissions)));

    /// <summary>
    /// Grants access to the messaging workspace itself. Which conversations it shows is decided by the conversation
    /// permissions below.
    /// </summary>
    public static readonly Permission UseMessagingWorkspace = new("UseMessagingWorkspace", LocalizationSource.Create("Use the messaging workspace", typeof(MessagingPermissions)));

    /// <summary>
    /// Grants every conversation, including those claimed by other agents and those no route assigned to an agent or
    /// a queue. This is the permission a conversation is authorized against.
    /// </summary>
    public static readonly Permission ViewAllConversations = new("ViewAllMessagingConversations", LocalizationSource.Create("View all messaging conversations", typeof(MessagingPermissions)));

    /// <summary>
    /// Grants the agent's own conversations and the unclaimed conversations of the queues they serve: the shared
    /// inbox they read, claim and answer from. A conversation another agent has claimed is never theirs.
    /// </summary>
    public static readonly Permission ViewQueueConversations = new("ViewQueueMessagingConversations", LocalizationSource.Create("View your own and your queues' unclaimed messaging conversations", typeof(MessagingPermissions)), [ViewAllConversations]);

    /// <summary>
    /// Grants the agent's own conversations: the ones assigned to them, and the ones sent to an endpoint they own that
    /// no colleague has claimed.
    /// </summary>
    public static readonly Permission ViewOwnConversations = new("ViewOwnMessagingConversations", LocalizationSource.Create("View your own messaging conversations", typeof(MessagingPermissions)), [ViewQueueConversations, ViewAllConversations]);

    /// <summary>
    /// Grants the ability to send outside the destination queue's business hours, in the contact's local time, on
    /// a channel that observes quiet hours. The quiet-hours guard warns rather than blocks, because a person who
    /// genuinely needs to reach a customer out of hours exists; this permission is what makes going ahead a
    /// decision somebody made.
    /// </summary>
    public static readonly Permission SendDuringQuietHours = new("SendMessagesDuringQuietHours", LocalizationSource.Create("Send messages outside business hours", typeof(MessagingPermissions)));

    /// <summary>
    /// Grants the ability to send a group message (broadcast) from the workspace.
    /// </summary>
    public static readonly Permission SendGroupMessages = new("SendGroupMessages", LocalizationSource.Create("Send group messages", typeof(MessagingPermissions)));
}
