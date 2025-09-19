using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Core;

public static class OmnichannelConstants
{
    public const string CollectionName = "Omnichannel";

    public const string AgentRole = "Agent";

    public static class NamedParts
    {
        public const string ContactMethods = "ContactMethods";
    }

    public static class Sterotypes
    {
        public const string OmnichannelContact = "OmnichannelContact";

        public const string OmnichannelSubject = "OmnichannelSubject";

        public const string ContactMethod = "ContactMethod";
    }

    public static class ContentParts
    {
        public const string OmnichannelContactInfo = "OmnichannelContactInfoPart";

        public const string EmailInfo = "EmailInfoPart";

        public const string PhoneNumberInfo = "PhoneNumberInfoPart";
    }

    public static class ContentTypes
    {
        public const string OmnichannelContact = "OmnichannelContact";

        public const string EmailAddress = "EmailAddress";

        public const string PhoneNumber = "PhoneNumber";
    }

    public static class Channels
    {
        public const string Phone = "Phone";

        public const string Sms = "SMS";

        public const string Email = "Email";
    }

    public static class Events
    {
        public const string SmsReceived = "SmsReceived";
    }

    public static class Features
    {
        public const string Area = "CrestApps.OrchardCore.Omnichannel";

        public const string AzureCommunicationServices = "CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices";

        public const string Managements = "CrestApps.OrchardCore.Omnichannel.Managements";
    }

    public static class Permissions
    {
        public readonly static Permission ListActivities = new("ListActivities", "List activities");

        public readonly static Permission ListContactActivities = new("ListContactActivities", "List Contact activities", [ListActivities]);

        public readonly static Permission ProcessActivity = new("ProcessActivity", "Process activity");

        public readonly static Permission ProcessOwnActivity = new("ProcessOwnActivity", "Process own activity");

        public readonly static Permission EditActivity = new("EditActivity", "Edit activity");

        public readonly static Permission ManageDispositions = new("ManageDispositions", "Manage dispositions");

        public readonly static Permission ManageCampaigns = new("ManageCampaigns", "Manage campaigns");

        public readonly static Permission ManageActivityBatches = new("ManageActivityBatches", "Manage activity batches");

        public readonly static Permission ManageChannelEndpoints = new("ManageChannelEndpoints", "Manage channel endpoints");
    }
}
