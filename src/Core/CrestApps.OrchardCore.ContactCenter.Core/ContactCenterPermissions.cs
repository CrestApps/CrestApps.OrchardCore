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
    /// Grants management of queue groups used for catalog organization and reporting.
    /// </summary>
    public static readonly Permission ManageQueueGroups = new("ManageContactCenterQueueGroups", "Manage Contact Center queue groups", [ManageQueues, ManageContactCenter]);

    /// <summary>
    /// Grants management of skills used by routing and agent sign-in.
    /// </summary>
    public static readonly Permission ManageSkills = new("ManageContactCenterSkills", "Manage Contact Center skills", [ManageContactCenter]);

    /// <summary>
    /// Grants management of business-hours calendars used to gate work distribution and automated sends. Declared so
    /// the Business Hours feature can be administered on its own, without enabling the full Work Distribution feature.
    /// </summary>
    public static readonly Permission ManageBusinessHours = new("ManageContactCenterBusinessHours", "Manage Contact Center business hours", [ManageContactCenter]);

    /// <summary>
    /// Grants management of dialer profiles and outbound dialing.
    /// </summary>
    public static readonly Permission ManageDialer = new("ManageContactCenterDialer", "Manage the Contact Center dialer", [ManageContactCenter]);

    /// <summary>
    /// Grants management of the reusable voice media library (hold music, greetings, and IVR prompts).
    /// </summary>
    public static readonly Permission ManageVoiceMedia = new("ManageContactCenterVoiceMedia", "Manage the Contact Center voice media library", [ManageContactCenter]);

    /// <summary>
    /// Grants an agent the ability to sign in to queues and campaigns and change their own presence.
    /// </summary>
    public static readonly Permission SignIntoQueues = new("ContactCenterSignIntoQueues", "Sign in to Contact Center queues and campaigns");

    /// <summary>
    /// Grants an agent the ability to pause and resume recording on their own live interaction to suppress
    /// capture while sensitive customer data (such as a payment card or a national identifier) is handled.
    /// </summary>
    public static readonly Permission SecurePauseRecording = new("ContactCenterSecurePauseRecording", "Pause recording on own live interactions", [SignIntoQueues]);

    /// <summary>
    /// Grants an agent the ability to start an agent-assisted secure capture of sensitive customer input on
    /// their own live interaction so the data is masked from the agent and never enters the recording.
    /// </summary>
    public static readonly Permission InitiateSecureCapture = new("ContactCenterInitiateSecureCapture", "Initiate secure capture on own live interactions", [SignIntoQueues]);

    /// <summary>
    /// Grants read-only, real-time visibility into queues, agents, and live interactions for supervisors.
    /// </summary>
    public static readonly Permission MonitorContactCenter = new("MonitorContactCenter", "Monitor the Contact Center in real time", [ManageContactCenter]);

    /// <summary>
    /// Grants permission to transfer live interactions to approved external destinations.
    /// </summary>
    public static readonly Permission TransferExternally = new("ContactCenterTransferExternally", "Transfer Contact Center calls externally", [MonitorContactCenter, ManageContactCenter]);

    /// <summary>
    /// Grants read-only access to the Contact Center historical reports and their exports.
    /// </summary>
    public static readonly Permission ViewReports = new("ViewContactCenterReports", "View Contact Center reports", [MonitorContactCenter, ManageContactCenter]);
}
