namespace CrestApps.OrchardCore.MailingLists.Core.Sync;

/// <summary>
/// Why a member change waits in the provider outbox, as the member's sync error shows it. A waiting change uses up no
/// attempt and goes out once the provider can take it.
/// </summary>
internal static class MailingListSyncWaits
{
    /// <summary>
    /// How long a waiting change waits before the outbox looks at its provider again.
    /// </summary>
    internal static readonly TimeSpan Delay = TimeSpan.FromMinutes(5);

    internal static string ProviderMissing(string providerName)
        => $"The provider '{providerName}' is not available because its feature is off. The change waits and is sent once the feature is on again.";

    internal static string ProviderNotConfigured(string providerName)
        => $"The provider '{providerName}' is not set up. The change waits and is sent once it is.";
}
