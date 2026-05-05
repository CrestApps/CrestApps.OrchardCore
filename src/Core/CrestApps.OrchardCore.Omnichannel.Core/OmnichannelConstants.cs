using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Core;

/// <summary>
/// Contains constant values for omnichannel.
/// </summary>
public static class OmnichannelConstants
{
    public const string CollectionName = "Omnichannel";

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

        public const string OmnichannelSubject = "OmnichannelSubject";

        public const string ContactMethod = "ContactMethod";
    }

    /// <summary>
    /// Represents the content parts.
    /// </summary>
    public static class ContentParts
    {
        // public const string OmnichannelContactInfo = "OmnichannelContactInfoPart";

        public const string OmnichannelContact = "OmnichannelContactPart";

        public const string EmailInfo = "EmailInfoPart";

        public const string PhoneNumberInfo = "PhoneNumberInfoPart";
    }

    /// <summary>
    /// Represents the content types.
    /// </summary>
    public static class ContentTypes
    {
        // public const string OmnichannelContact = "OmnichannelContact";

        public const string EmailAddress = "EmailAddress";

        public const string PhoneNumber = "PhoneNumber";
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
    }

    /// <summary>
    /// Represents the events.
    /// </summary>
    public static class Events
    {
        public const string SmsReceived = "SmsReceived";
    }

    /// <summary>
    /// Represents the features.
    /// </summary>
    public static class Features
    {
        public const string Area = "CrestApps.OrchardCore.Omnichannel";

        public const string AzureCommunicationServices = "CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices";

        public const string Managements = "CrestApps.OrchardCore.Omnichannel.Managements";
    }

    /// <summary>
    /// Represents the permissions.
    /// </summary>
    public static class Permissions
    {
        /// <summary>
        /// Gets the permission to list activities.
        /// </summary>
        public readonly static Permission ListActivities = new("ListActivities", "List activities");

        /// <summary>
        /// Gets the permission to list contact activities.
        /// </summary>
        public readonly static Permission ListContactActivities = new("ListContactActivities", "List Contact activities", [ListActivities]);

        /// <summary>
        /// Gets the permission to complete an activity.
        /// </summary>
        public readonly static Permission CompleteActivity = new("CompleteActivity", "Complete activity");

        /// <summary>
        /// Gets the permission to complete own activities.
        /// </summary>
        public readonly static Permission CompleteOwnActivity = new("CompleteOwnActivity", "Complete own activity");

        /// <summary>
        /// Gets the permission to edit an activity.
        /// </summary>
        public readonly static Permission EditActivity = new("EditActivity", "Edit activity");

        /// <summary>
        /// Gets the permission to manage dispositions.
        /// </summary>
        public readonly static Permission ManageDispositions = new("ManageDispositions", "Manage dispositions");

        /// <summary>
        /// Gets the permission to manage campaigns.
        /// </summary>
        public readonly static Permission ManageCampaigns = new("ManageCampaigns", "Manage campaigns");

        /// <summary>
        /// Gets the permission to manage activities in bulk.
        /// </summary>
        public readonly static Permission ManageActivities = new("ManageActivities", "Manage activities");

        /// <summary>
        /// Gets the permission to manage activity batches.
        /// </summary>
        public readonly static Permission ManageActivityBatches = new("ManageActivityBatches", "Manage activity batches");

        /// <summary>
        /// Gets the permission to manage channel endpoints.
        /// </summary>
        public readonly static Permission ManageChannelEndpoints = new("ManageChannelEndpoints", "Manage channel endpoints");
    }
}
