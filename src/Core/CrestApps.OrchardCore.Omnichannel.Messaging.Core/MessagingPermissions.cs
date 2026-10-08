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
/// The list passed to each permission is the permissions that imply it, not the ones it needs. Using the workspace
/// therefore implies none of the others: an agent who may use it sees only their own conversations until a role also
/// grants them their queues' unclaimed conversations, every conversation, or group messages.
/// </remarks>
public static class MessagingPermissions
{
    /// <summary>
    /// Grants management of the workspace's shared configuration: canned-response templates and the inbound
    /// routing configured on each messaging endpoint.
    /// </summary>
    public static readonly Permission ManageMessaging = new("ManageMessaging", LocalizationSource.Create("Manage the messaging workspace", typeof(MessagingPermissions)));

    /// <summary>
    /// Grants a supervisor visibility of every conversation, including those claimed by other agents and those no
    /// route assigned to an agent or a queue.
    /// </summary>
    public static readonly Permission ViewAllConversations = new("ViewAllMessagingConversations", LocalizationSource.Create("View all messaging conversations", typeof(MessagingPermissions)));

    /// <summary>
    /// Grants an agent the unclaimed conversations of the queues they serve: the shared inbox they read, claim and
    /// answer from. A conversation another agent has claimed is never theirs to read.
    /// </summary>
    public static readonly Permission ViewQueueConversations = new("ViewQueueMessagingConversations", LocalizationSource.Create("View unclaimed messaging conversations in your queues", typeof(MessagingPermissions)), [ViewAllConversations]);

    /// <summary>
    /// Grants an agent access to the messaging workspace and to their own conversations: the ones assigned to them
    /// and the ones sent to the endpoints they own, unless another agent has claimed them.
    /// </summary>
    public static readonly Permission UseMessagingWorkspace = new("UseMessagingWorkspace", LocalizationSource.Create("Use the messaging workspace and view your own conversations", typeof(MessagingPermissions)), [ViewQueueConversations, ViewAllConversations]);

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
