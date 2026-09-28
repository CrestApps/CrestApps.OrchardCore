using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core;

/// <summary>
/// The permissions exposed by the Omnichannel Messaging workspace. They apply to every messaging channel, so a
/// person allowed to use the workspace can work a conversation whatever channel it runs on. Queue membership
/// itself is governed by the existing Contact Center agent entitlements, so there is no parallel membership
/// permission here.
/// </summary>
public static class MessagingPermissions
{
    /// <summary>
    /// Grants management of the workspace's shared configuration: canned-response templates and the inbound
    /// routing configured on each messaging endpoint.
    /// </summary>
    public static readonly Permission ManageMessaging = new("ManageMessaging", "Manage the messaging workspace");

    /// <summary>
    /// Grants an agent access to the messaging workspace to send and receive on the endpoints they own or serve.
    /// </summary>
    public static readonly Permission UseMessagingWorkspace = new("UseMessagingWorkspace", "Use the messaging workspace");

    /// <summary>
    /// Grants the ability to send outside the destination queue's business hours, in the contact's local time, on
    /// a channel that observes quiet hours. The quiet-hours guard warns rather than blocks, because a person who
    /// genuinely needs to reach a customer out of hours exists; this permission is what makes going ahead a
    /// decision somebody made.
    /// </summary>
    public static readonly Permission SendDuringQuietHours = new("SendMessagesDuringQuietHours", "Send messages outside business hours", [UseMessagingWorkspace]);

    /// <summary>
    /// Grants the ability to send a group message (broadcast) from the workspace.
    /// </summary>
    public static readonly Permission SendGroupMessages = new("SendGroupMessages", "Send group messages", [UseMessagingWorkspace]);

    /// <summary>
    /// Grants a supervisor visibility of every conversation, not only their own or their queue's.
    /// </summary>
    public static readonly Permission ViewAllConversations = new("ViewAllMessagingConversations", "View all messaging conversations", [UseMessagingWorkspace]);
}
