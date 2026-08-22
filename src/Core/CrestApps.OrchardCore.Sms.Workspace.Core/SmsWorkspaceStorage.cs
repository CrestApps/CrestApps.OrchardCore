namespace CrestApps.OrchardCore.Sms.Workspace.Core;

/// <summary>
/// Internal storage and schema constants for the SMS Communication Portal module set. Deliberately kept out
/// of the public Abstractions package so a schema change does not force a public-package version bump.
/// </summary>
internal static class SmsWorkspaceStorage
{
    /// <summary>
    /// The YesSql collection name used to store SMS portal documents in isolation from other modules.
    /// </summary>
    public const string CollectionName = "SmsWorkspace";

    /// <summary>
    /// The maximum stored length, in characters, of a phone number (DID or customer address) across the SMS
    /// portal index tables.
    /// </summary>
    public const int AddressLength = 255;

    /// <summary>
    /// The maximum stored length, in characters, of a provider technical name.
    /// </summary>
    public const int ProviderNameLength = 128;
}
