using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Core;

/// <summary>
/// Contains constant values for omnichannel.
/// </summary>
public static class OmnichannelConstants
{
    public const string CollectionName = "Omnichannel";

    /// <summary>
    /// The name of the disposition applied to a call that found the number not in service when the subject flow wires
    /// no disposition to an action that marks the number. It is created the first time it is needed.
    /// </summary>
    public const string NotInServiceDispositionName = "Number Not In Service";

    public const string AgentRole = "Agent";

    public const string CompleteActivityGroup = "complete";

    /// <summary>
    /// Represents the named parts.
    /// </summary>
    public static class NamedParts
    {
        public const string ContactMethods = "ContactMethods";
    }

    /// <summary>
    /// Represents the sterotypes.
    /// </summary>
    public static class Sterotypes
    {
        public const string OmnichannelContact = "OmnichannelContact";

        public const string ContactMethod = "ContactMethod";
    }

    /// <summary>
    /// Represents the content parts.
    /// </summary>
    public static class ContentParts
    {
        // public const string OmnichannelContactInfo = "OmnichannelContactInfoPart";

        public const string OmnichannelContact = "OmnichannelContactPart";

        public const string OmnichannelSubject = "OmnichannelSubjectPart";

        public const string EmailInfo = "EmailInfoPart";

        public const string PhoneNumberInfo = "PhoneNumberInfoPart";

        /// <summary>
        /// The part that marks a contact-capable content type as a lead: a prospect that can be reached like a
        /// contact but is kept apart from contacts until it is converted.
        /// </summary>
        public const string Lead = "LeadPart";

        /// <summary>
        /// The part that marks a content type as an account, the container of contacts and opportunities.
        /// </summary>
        public const string Account = "AccountPart";

        /// <summary>
        /// The part that marks a content type as an opportunity, a deal in progress on an account.
        /// </summary>
        public const string Opportunity = "OpportunityPart";

        /// <summary>
        /// The Orchard Core list part, which an account uses to contain its contacts and opportunities.
        /// </summary>
        public const string List = "ListPart";

        /// <summary>
        /// The Orchard Core part a contained content item carries to point at its list.
        /// </summary>
        public const string Contained = "ContainedPart";
    }

    /// <summary>
    /// Represents the content types.
    /// </summary>
    public static class ContentTypes
    {
        // public const string OmnichannelContact = "OmnichannelContact";

        public const string EmailAddress = "EmailAddress";

        public const string PhoneNumber = "PhoneNumber";

        /// <summary>
        /// The account content type the CRM feature creates.
        /// </summary>
        public const string Account = "Account";

        /// <summary>
        /// The lead source content type the CRM feature creates. Its items are the sources a lead or an opportunity
        /// can come from, for example Web form or Trade show.
        /// </summary>
        public const string LeadSource = "LeadSource";
    }

    /// <summary>
    /// Represents the channels.
    /// </summary>
    public static class Channels
    {
        public const string Phone = "Phone";

        public const string Sms = "SMS";

        public const string Email = "Email";
    }

    public static class ActionTypes
    {
        public const string Finish = "Finish";

        public const string TryAgain = "TryAgain";

        public const string NewActivity = "NewActivity";

        public const string ConvertLead = "ConvertLead";
    }

    /// <summary>
    /// Represents the events.
    /// </summary>
    public static class Events
    {
        public const string SmsReceived = "SmsReceived";
    }

    /// <summary>
    /// Well-known stable reason codes recorded on <see cref="Models.OmnichannelActivity.TerminalReasonCode"/>
    /// when an automated activity reaches a terminal state.
    /// </summary>
    public static class TerminalReasons
    {
        /// <summary>
        /// The automated conversation was concluded because it was handed off to a live human agent.
        /// </summary>
        public const string HandedOffToAgent = "handed_off_to_agent";

        /// <summary>
        /// The automated conversation could not be routed live (for example the destination queue was closed
        /// after hours), so a callback was scheduled instead.
        /// </summary>
        public const string HandedOffAfterHoursCallback = "handed_off_after_hours_callback";

        /// <summary>
        /// The number the activity was reaching is not in service. A completed activity with this reason was dialed
        /// and the network reported the number unallocated, disconnected or invalid, so it was dispositioned
        /// automatically without an agent or the AI; a cancelled one was not dialed at all because the number was
        /// already known to be out of service.
        /// </summary>
        public const string NumberNotInService = "number_not_in_service";

        /// <summary>
        /// The activity was open when its lead was converted, and the conversion was asked to cancel open work.
        /// </summary>
        public const string LeadConverted = "lead_converted";

        /// <summary>
        /// The set of terminal reason codes that count as an escalation to a human for reporting.
        /// </summary>
        public static bool IsHandoff(string terminalReasonCode)
            => terminalReasonCode is HandedOffToAgent or HandedOffAfterHoursCallback;
    }

    /// <summary>
    /// What found a phone number to be out of service, recorded on <see cref="Models.NotInServiceNumber.Source"/>.
    /// </summary>
    public static class NotInServiceSources
    {
        /// <summary>
        /// The Contact Center dialer dialed the number and the network said it is not in service.
        /// </summary>
        public const string Dialer = "Dialer";

        /// <summary>
        /// An automated (AI) call dialed the number and the network said it is not in service.
        /// </summary>
        public const string AutomatedCall = "AutomatedCall";

        /// <summary>
        /// An agent dispositioned the call with a disposition whose subject action marks the number.
        /// </summary>
        public const string Agent = "Agent";

        /// <summary>
        /// A phone number lookup reported the line as disconnected or the number as invalid.
        /// </summary>
        public const string Lookup = "Lookup";

        /// <summary>
        /// A person marked the number by hand.
        /// </summary>
        public const string Manual = "Manual";
    }

    /// <summary>
    /// Provider-neutral reasons an SMS provider refused a message. Each provider maps its own codes onto these,
    /// so the code that reacts to a refusal never needs to know which carrier sent it.
    /// </summary>
    public static class SmsErrorCodes
    {
        /// <summary>
        /// The recipient has opted out of messages from the sending number with the provider or the carrier (for
        /// example by texting STOP), so the provider refuses to deliver to them and has confirmed the opt-out to
        /// them itself. Retrying cannot succeed; the recipient is to be recorded as opted out.
        /// </summary>
        public const string RecipientOptedOut = "recipient_opted_out";

        /// <summary>
        /// The message carries pictures and the provider serving the sending number cannot send picture messages.
        /// Retrying cannot succeed.
        /// </summary>
        public const string MediaNotSupported = "media_not_supported";
    }

    /// <summary>
    /// Represents the features.
    /// </summary>
    public static class Features
    {
        public const string Area = "CrestApps.OrchardCore.Omnichannel";

        public const string AzureCommunicationServices = "CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices";

        public const string Activities = "CrestApps.OrchardCore.Omnichannel.Activities";

        public const string ChannelEndpoints = "CrestApps.OrchardCore.Omnichannel.ChannelEndpoints";

        public const string Managements = "CrestApps.OrchardCore.Omnichannel.Managements";

        public const string Crm = "CrestApps.OrchardCore.Omnichannel.Crm";
    }

    /// <summary>
    /// Represents the permissions.
    /// </summary>
    public static class Permissions
    {
        /// <summary>
        /// Gets the permission to list activities.
        /// </summary>
        public readonly static Permission ListActivities = new("ListActivities", LocalizationSource.Create("List activities", typeof(Permissions)));

        /// <summary>
        /// Gets the permission to list contact activities.
        /// </summary>
        public readonly static Permission ListContactActivities = new("ListContactActivities", LocalizationSource.Create("List Contact activities", typeof(Permissions)), [ListActivities]);

        /// <summary>
        /// Gets the permission to complete an activity.
        /// </summary>
        public readonly static Permission CompleteActivity = new("CompleteActivity", LocalizationSource.Create("Complete activity", typeof(Permissions)));

        /// <summary>
        /// Gets the permission to complete own activities.
        /// </summary>
        public readonly static Permission CompleteOwnActivity = new("CompleteOwnActivity", LocalizationSource.Create("Complete own activity", typeof(Permissions)));

        /// <summary>
        /// Gets the permission to manage dispositions.
        /// </summary>
        public readonly static Permission ManageDispositions = new("ManageDispositions", LocalizationSource.Create("Manage dispositions", typeof(Permissions)));

        /// <summary>
        /// Gets the permission to manage campaigns.
        /// </summary>
        public readonly static Permission ManageCampaigns = new("ManageCampaigns", LocalizationSource.Create("Manage campaigns", typeof(Permissions)));

        /// <summary>
        /// Gets the permission to manage re-engagement (nudge) schedules.
        /// </summary>
        public readonly static Permission ManageCadences = new("ManageCadences", LocalizationSource.Create("Manage cadences", typeof(Permissions)));

        /// <summary>
        /// Gets the permission to manage campaign groups.
        /// </summary>
        public readonly static Permission ManageCampaignGroups = new("ManageCampaignGroups", LocalizationSource.Create("Manage campaign groups", typeof(Permissions)));

        /// <summary>
        /// Gets the permission to manage activities in bulk.
        /// </summary>
        public readonly static Permission ManageActivities = new("ManageActivities", LocalizationSource.Create("Manage activities", typeof(Permissions)));

        /// <summary>
        /// Gets the permission to create and edit activities. Anyone who can manage activities can also edit them.
        /// Declared after <see cref="ManageActivities"/> because static fields initialize in declaration order.
        /// </summary>
        public readonly static Permission EditActivity = new("EditActivity", LocalizationSource.Create("Create and edit activities", typeof(Permissions)), [ManageActivities]);

        /// <summary>
        /// Gets the permission to purge an activity.
        /// </summary>
        public readonly static Permission PurgeActivity = new("PurgeActivity", LocalizationSource.Create("Purge activity", typeof(Permissions)), [ManageActivities]);

        /// <summary>
        /// Gets the permission to manage activity batches.
        /// </summary>
        public readonly static Permission ManageActivityBatches = new("ManageActivityBatches", LocalizationSource.Create("Manage activity batches", typeof(Permissions)));

        /// <summary>
        /// Gets the permission to delete an activity batch that has finished loading. The activities it created are kept.
        /// </summary>
        public readonly static Permission DeleteLoadedActivityBatches = new("DeleteLoadedActivityBatches", LocalizationSource.Create("Delete loaded activity batches", typeof(Permissions)));

        /// <summary>
        /// Gets the permission to manage channel endpoints.
        /// </summary>
        public readonly static Permission ManageChannelEndpoints = new("ManageChannelEndpoints", LocalizationSource.Create("Manage omnichannel addresses", typeof(Permissions)));

        /// <summary>
        /// Gets the permission to manage subject flows.
        /// </summary>
        public readonly static Permission ManageSubjectFlows = new("ManageSubjectFlows", LocalizationSource.Create("Manage subject flows", typeof(Permissions)));

        /// <summary>
        /// Gets the permission to view the Omnichannel reports.
        /// </summary>
        public readonly static Permission ViewReports = new("ViewOmnichannelReports", LocalizationSource.Create("View Omnichannel reports", typeof(Permissions)), [ManageActivities]);

        /// <summary>
        /// Gets the permission to convert a lead into a contact.
        /// </summary>
        public readonly static Permission ConvertLead = new("ConvertLead", LocalizationSource.Create("Convert leads", typeof(Permissions)), [ManageActivities]);

        /// <summary>
        /// Gets the permission to edit a lead after it was converted, for correcting its record only.
        /// </summary>
        public readonly static Permission EditConvertedLead = new("EditConvertedLead", LocalizationSource.Create("Edit converted leads", typeof(Permissions)));

        /// <summary>
        /// Gets the permission to manage lead statuses.
        /// </summary>
        public readonly static Permission ManageLeadStatuses = new("ManageLeadStatuses", LocalizationSource.Create("Manage lead statuses", typeof(Permissions)));

        /// <summary>
        /// Gets the permission to manage opportunity stages.
        /// </summary>
        public readonly static Permission ManageOpportunityStages = new("ManageOpportunityStages", LocalizationSource.Create("Manage opportunity stages", typeof(Permissions)));
    }
}
