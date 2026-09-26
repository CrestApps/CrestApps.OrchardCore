namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core;

/// <summary>
/// Internal storage and schema constants for the Omnichannel Messaging module set. Deliberately kept out of the
/// public Abstractions package so a schema change does not force a public-package version bump.
/// </summary>
internal static class MessagingStorage
{
    /// <summary>
    /// The YesSql collection name used to store messaging workspace documents in isolation from other modules.
    /// </summary>
    /// <remarks>
    /// The collection name is the physical table prefix, so renaming it orphans every stored conversation,
    /// broadcast and template. It is an internal storage detail that never surfaces in the UI &#8212; leave it alone.
    /// </remarks>
    public const string CollectionName = "OmnichannelMessaging";

    /// <summary>
    /// The collection the SMS-only portal that preceded the workspace stored its documents in. Read once, by the
    /// migration that imports them.
    /// </summary>
    public const string LegacySmsPortalCollectionName = "SmsWorkspace";

    /// <summary>
    /// The maximum stored length, in characters, of an address (our endpoint or the contact) across the workspace
    /// index tables.
    /// </summary>
    public const int AddressLength = 255;

    /// <summary>
    /// The maximum stored length, in characters, of a channel technical name.
    /// </summary>
    public const int ChannelLength = 50;

    /// <summary>
    /// The maximum stored length, in characters, of a customer key: a prefix, the channel and an address.
    /// </summary>
    public const int CustomerKeyLength = 320;

    /// <summary>
    /// The unique index that keeps one conversation per address pair on a channel. It is what makes a concurrent
    /// create fail loudly instead of producing a second thread for the same contact.
    /// </summary>
    public const string ConversationAddressesUniqueIndexName = "UQ_MessagingConversationIndex_Addresses";
}
