using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Core;

/// <summary>
/// Defines the permissions exposed by the base Contact Center feature.
/// </summary>
public static class ContactCenterPermissions
{
    /// <summary>
    /// Grants full management of the Contact Center, including configuration and every interaction.
    /// </summary>
    public static readonly Permission ManageContactCenter = new("ManageContactCenter", "Manage the Contact Center");

    /// <summary>
    /// Grants management of interactions.
    /// </summary>
    public static readonly Permission ManageInteractions = new("ManageInteractions", "Manage interactions", [ManageContactCenter]);

    /// <summary>
    /// Grants read-only access to interactions.
    /// </summary>
    public static readonly Permission ViewInteractions = new("ViewInteractions", "View interactions", [ManageInteractions, ManageContactCenter]);

    /// <summary>
    /// Grants management of agent profiles, presence, and queue membership.
    /// </summary>
    public static readonly Permission ManageAgents = new("ManageContactCenterAgents", "Manage Contact Center agents", [ManageContactCenter]);

    /// <summary>
    /// Grants management of queues, queue items, and assignment.
    /// </summary>
    public static readonly Permission ManageQueues = new("ManageContactCenterQueues", "Manage Contact Center queues", [ManageContactCenter]);

    /// <summary>
    /// Grants management of skills used by routing and agent sign-in.
    /// </summary>
    public static readonly Permission ManageSkills = new("ManageContactCenterSkills", "Manage Contact Center skills", [ManageContactCenter]);

    /// <summary>
    /// Grants management of dialer profiles and outbound dialing.
    /// </summary>
    public static readonly Permission ManageDialer = new("ManageContactCenterDialer", "Manage the Contact Center dialer", [ManageContactCenter]);

    /// <summary>
    /// Grants an agent the ability to sign in to queues and campaigns and change their own presence.
    /// </summary>
    public static readonly Permission SignIntoQueues = new("ContactCenterSignIntoQueues", "Sign in to Contact Center queues and campaigns");

    /// <summary>
    /// Grants read-only, real-time visibility into queues, agents, and live interactions for supervisors.
    /// </summary>
    public static readonly Permission MonitorContactCenter = new("MonitorContactCenter", "Monitor the Contact Center in real time", [ManageContactCenter]);

    /// <summary>
    /// Grants read-only access to the Contact Center historical reports and their exports.
    /// </summary>
    public static readonly Permission ViewReports = new("ViewContactCenterReports", "View Contact Center reports", [MonitorContactCenter, ManageContactCenter]);
}
