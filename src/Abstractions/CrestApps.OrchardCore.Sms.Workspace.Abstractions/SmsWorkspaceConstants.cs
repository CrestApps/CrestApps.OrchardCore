namespace CrestApps.OrchardCore.Sms.Workspace;

/// <summary>
/// Contains stable constants shared across the SMS Communication Portal module set: feature identifiers, the
/// channel name, and integration event names. Storage and schema details are kept internal to the Core and
/// module assemblies so they never force a public-package version bump.
/// </summary>
public static class SmsWorkspaceConstants
{
    /// <summary>
    /// The channel value the SMS portal works with. Matches the existing Omnichannel SMS channel so a single
    /// number catalog and message store are shared between the automated and human paths.
    /// </summary>
    public const string Channel = "SMS";

    /// <summary>
    /// Feature identifiers for the SMS portal module set.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The SMS Communication Portal feature: conversations, number routing, the inbox workspace, and the
        /// real-time messaging hub.
        /// </summary>
        public const string Workspace = "CrestApps.OrchardCore.Sms.Workspace";
    }

    /// <summary>
    /// The technical names of the built-in SMS providers a number can be pinned to.
    /// </summary>
    public static class Providers
    {
        public const string Twilio = "Twilio";

        public const string Telnyx = "Telnyx";

        public const string AzureCommunicationServices = "AzureCommunicationServices";
    }
}
