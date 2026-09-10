namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core;

/// <summary>
/// Internal storage and schema constants for the SMS Communication Portal module set. Deliberately kept out
/// of the public Abstractions package so a schema change does not force a public-package version bump.
/// </summary>
internal static class SmsPortalStorage
{
    /// <summary>
    /// The YesSql collection name used to store SMS portal documents in isolation from other modules.
    /// </summary>
    /// <remarks>
    /// Deliberately still <c>SmsWorkspace</c>. The collection name is the physical table prefix, so renaming it
    /// to match the module's new <c>Portal</c> name would orphan every stored conversation, broadcast and
    /// template. It is an internal storage detail that never surfaces in the UI &#8212; leave it alone.
    /// </remarks>
    public const string CollectionName = "SmsWorkspace";

    /// <summary>
    /// The maximum stored length, in characters, of a phone number (DID or contact address) across the SMS
    /// portal index tables.
    /// </summary>
    public const int AddressLength = 255;

    /// <summary>
    /// The maximum stored length, in characters, of a provider technical name.
    /// </summary>
    public const int ProviderNameLength = 128;

    /// <summary>
    /// The unique index that keeps one conversation per number pair. It is what makes a concurrent create fail
    /// loudly instead of producing a second thread for the same contact.
    /// </summary>
    public const string ConversationAddressesUniqueIndexName = "UQ_SmsConversationIndex_Addresses";
}
