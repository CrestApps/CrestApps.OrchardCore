using OrchardCore.Localization;
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
    public static readonly Permission ManageContactCenter = new("ManageContactCenter", LocalizationSource.Create("Manage the Contact Center", typeof(ContactCenterPermissions)));

    /// <summary>
    /// Grants management of interactions.
    /// </summary>
    public static readonly Permission ManageInteractions = new("ManageInteractions", LocalizationSource.Create("Manage interactions", typeof(ContactCenterPermissions)), [ManageContactCenter]);

    /// <summary>
    /// Grants read-only access to interactions.
    /// </summary>
    public static readonly Permission ViewInteractions = new("ViewInteractions", LocalizationSource.Create("View interactions", typeof(ContactCenterPermissions)), [ManageInteractions, ManageContactCenter]);

    /// <summary>
    /// Grants management of agent profiles, presence, and queue membership.
    /// </summary>
    public static readonly Permission ManageAgents = new("ManageContactCenterAgents", LocalizationSource.Create("Manage Contact Center agents", typeof(ContactCenterPermissions)), [ManageContactCenter]);

    /// <summary>
    /// Grants management of queues, queue items, and assignment.
    /// </summary>
    public static readonly Permission ManageQueues = new("ManageContactCenterQueues", LocalizationSource.Create("Manage Contact Center queues", typeof(ContactCenterPermissions)), [ManageContactCenter]);

    /// <summary>
    /// Grants management of queue groups used for catalog organization and reporting.
    /// </summary>
    public static readonly Permission ManageQueueGroups = new("ManageContactCenterQueueGroups", LocalizationSource.Create("Manage Contact Center queue groups", typeof(ContactCenterPermissions)), [ManageQueues, ManageContactCenter]);

    /// <summary>
    /// Grants management of skills used by routing and agent sign-in.
    /// </summary>
    public static readonly Permission ManageSkills = new("ManageContactCenterSkills", LocalizationSource.Create("Manage Contact Center skills", typeof(ContactCenterPermissions)), [ManageContactCenter]);

    /// <summary>
    /// Grants management of business-hours calendars used to gate work distribution and automated sends. Declared so
    /// the Business Hours feature can be administered on its own, without enabling the full Work Distribution feature.
    /// </summary>
    public static readonly Permission ManageBusinessHours = new("ManageContactCenterBusinessHours", LocalizationSource.Create("Manage Contact Center business hours", typeof(ContactCenterPermissions)), [ManageContactCenter]);

    /// <summary>
    /// Grants management of dialer profiles and outbound dialing.
    /// </summary>
    public static readonly Permission ManageDialer = new("ManageContactCenterDialer", LocalizationSource.Create("Manage the Contact Center dialer", typeof(ContactCenterPermissions)), [ManageContactCenter]);

    /// <summary>
    /// Grants management of the reusable voice media library (hold music, greetings, and IVR prompts).
    /// </summary>
    public static readonly Permission ManageVoiceMedia = new("ManageContactCenterVoiceMedia", LocalizationSource.Create("Manage the Contact Center voice media library", typeof(ContactCenterPermissions)), [ManageContactCenter]);

    /// <summary>
    /// Grants an agent the ability to sign in to queues and campaigns and change their own presence.
    /// </summary>
    public static readonly Permission SignIntoQueues = new("ContactCenterSignIntoQueues", LocalizationSource.Create("Sign in to Contact Center queues and campaigns", typeof(ContactCenterPermissions)));

    /// <summary>
    /// Grants an agent the ability to pause and resume recording on their own live interaction to suppress
    /// capture while sensitive customer data (such as a payment card or a national identifier) is handled.
    /// </summary>
    public static readonly Permission SecurePauseRecording = new("ContactCenterSecurePauseRecording", LocalizationSource.Create("Pause recording on own live interactions", typeof(ContactCenterPermissions)), [SignIntoQueues]);

    /// <summary>
    /// Grants an agent the ability to start an agent-assisted secure capture of sensitive customer input on
    /// their own live interaction so the data is masked from the agent and never enters the recording.
    /// </summary>
    public static readonly Permission InitiateSecureCapture = new("ContactCenterInitiateSecureCapture", LocalizationSource.Create("Initiate secure capture on own live interactions", typeof(ContactCenterPermissions)), [SignIntoQueues]);

    /// <summary>
    /// Grants read-only, real-time visibility into queues, agents, and live interactions for supervisors.
    /// </summary>
    public static readonly Permission MonitorContactCenter = new("MonitorContactCenter", LocalizationSource.Create("Monitor the Contact Center in real time", typeof(ContactCenterPermissions)), [ManageContactCenter]);

    /// <summary>
    /// Grants permission to transfer live interactions to approved external destinations.
    /// </summary>
    public static readonly Permission TransferExternally = new("ContactCenterTransferExternally", LocalizationSource.Create("Transfer Contact Center calls externally", typeof(ContactCenterPermissions)), [MonitorContactCenter, ManageContactCenter]);

    /// <summary>
    /// Grants read-only access to the Contact Center historical reports and their exports.
    /// </summary>
    public static readonly Permission ViewReports = new("ViewContactCenterReports", LocalizationSource.Create("View Contact Center reports", typeof(ContactCenterPermissions)), [MonitorContactCenter, ManageContactCenter]);

    /// <summary>
    /// Grants a supervisor the interventions that change a live call or an agent rather than only observe them: taking
    /// a call over, ending it, transferring it, turning its recording on or off, and setting an agent's state. Watching,
    /// coaching and joining a call, and messaging an agent, need only <see cref="MonitorContactCenter"/>.
    /// </summary>
    public static readonly Permission InterveneInCalls = new("ContactCenterInterveneInCalls", LocalizationSource.Create("Take over, end, transfer and record live Contact Center calls, and set agents' state", typeof(ContactCenterPermissions)), [ManageContactCenter]);

    /// <summary>
    /// Grants the management of queue shared voicemail boxes beyond one's own handling: deleting a message, and
    /// returning or resolving a message somebody else has claimed. It is still limited to the queues the user is
    /// entitled to, unless they also hold <see cref="ManageContactCenter"/>.
    /// </summary>
    public static readonly Permission ManageSharedVoicemail = new("ManageContactCenterSharedVoicemail", LocalizationSource.Create("Manage shared queue voicemail: delete messages and take over other users' claims", typeof(ContactCenterPermissions)), [ManageContactCenter]);

    /// <summary>
    /// Grants access to the shared voicemail boxes of the queues the user is entitled to: listening to the messages,
    /// claiming them, marking them as dealt with, and calling the callers back. A user sees a queue's box only when
    /// they also hold that queue in their agent entitlements, unless they hold <see cref="ManageContactCenter"/>.
    /// </summary>
    public static readonly Permission AccessSharedVoicemail = new("AccessContactCenterSharedVoicemail", LocalizationSource.Create("Access shared queue voicemail for entitled queues", typeof(ContactCenterPermissions)), [ManageSharedVoicemail, ManageContactCenter]);

    /// <summary>
    /// Grants searching, listing and listening to every user's call recordings, and reading their transcripts.
    /// </summary>
    public static readonly Permission ListenToAllCallRecordings = new("ListenToAllCallRecordings", LocalizationSource.Create("Listen to anyone's call recordings", typeof(ContactCenterPermissions)), [ManageContactCenter]);

    /// <summary>
    /// Grants searching, listing and listening to the recordings of one's own calls, and reading their transcripts. A
    /// user who holds only this sees no other user's calls.
    /// </summary>
    public static readonly Permission ListenToOwnCallRecordings = new("ListenToOwnCallRecordings", LocalizationSource.Create("Listen to own call recordings", typeof(ContactCenterPermissions)), [ListenToAllCallRecordings, ManageContactCenter]);

    /// <summary>
    /// Grants erasing a call recording the user may listen to. Deliberately implied by no other permission, managing
    /// the contact center included: deleting a recording cannot be undone, so it is granted on its own.
    /// </summary>
    public static readonly Permission DeleteCallRecordings = new("DeleteCallRecordings", LocalizationSource.Create("Delete call recordings", typeof(ContactCenterPermissions)));
}
