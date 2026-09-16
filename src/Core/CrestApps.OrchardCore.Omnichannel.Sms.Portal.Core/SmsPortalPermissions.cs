using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core;

/// <summary>
/// The permissions exposed by the SMS Communication Portal. Queue membership itself is governed by the
/// existing Contact Center agent entitlements, so there is no parallel membership permission here.
/// </summary>
public static class SmsPortalPermissions
{
    /// <summary>
    /// Grants management of the DID-to-agent/queue number routes.
    /// </summary>
    public static readonly Permission ManageSmsNumberRoutes = new("ManageSmsNumberRoutes", "Manage SMS number routes");

    /// <summary>
    /// Grants an agent access to the SMS portal to send and receive on the numbers they own or serve.
    /// </summary>
    public static readonly Permission UseSmsPortal = new("UseSmsPortal", "Use the SMS portal");

    /// <summary>
    /// Grants the ability to send outside the destination queue's business hours, in the contact's local time.
    /// The quiet-hours guard warns rather than blocks, because a person who genuinely needs to reach a customer
    /// out of hours exists; this permission is what makes going ahead a decision somebody made.
    /// </summary>
    public static readonly Permission SendDuringQuietHours = new("SendSmsDuringQuietHours", "Send SMS outside business hours", [UseSmsPortal]);

    /// <summary>
    /// Grants the ability to send group SMS (broadcast) from the portal.
    /// </summary>
    public static readonly Permission SendGroupSms = new("SendGroupSms", "Send group SMS", [UseSmsPortal]);

    /// <summary>
    /// Grants a supervisor visibility of every conversation, not only their own or their queue's.
    /// </summary>
    public static readonly Permission ViewAllConversations = new("ViewAllSmsConversations", "View all SMS conversations", [UseSmsPortal]);
}
