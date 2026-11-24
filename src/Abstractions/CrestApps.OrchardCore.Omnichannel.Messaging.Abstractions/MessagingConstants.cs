namespace CrestApps.OrchardCore.Omnichannel.Messaging;

/// <summary>
/// Contains stable constants shared across the Omnichannel Messaging module set. Storage and schema details are
/// kept internal to the Core and module assemblies so they never force a public-package version bump.
/// </summary>
public static class MessagingConstants
{
    /// <summary>
    /// Feature identifiers for the messaging workspace module set.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The Omnichannel Messaging workspace feature: the channel-agnostic inbox, conversations, routing and the
        /// real-time hub every messaging channel plugs into. It carries no channel of its own; a channel feature
        /// such as SMS is what gives it something to send and receive on.
        /// </summary>
        public const string Workspace = "CrestApps.OrchardCore.Omnichannel.Messaging";

        /// <summary>
        /// The routed (push) distribution feature: assigns a new department conversation to a specific eligible
        /// agent through Contact Center work distribution, and re-pools threads nobody picks up. Split from the
        /// workspace because it is the only part that needs the Work Distribution feature.
        /// </summary>
        public const string RoutedDistribution = "CrestApps.OrchardCore.Omnichannel.Messaging.RoutedDistribution";

        /// <summary>
        /// The SMS channel feature: sends and receives text messages in the messaging workspace.
        /// </summary>
        public const string Sms = "CrestApps.OrchardCore.Omnichannel.Messaging.Sms";
    }
}
