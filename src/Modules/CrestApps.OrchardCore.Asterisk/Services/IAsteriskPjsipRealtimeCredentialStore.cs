namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Materializes short-lived browser SIP users into Asterisk PJSIP Realtime storage.
/// The Asterisk adapter chooses PJSIP Realtime instead of static pre-provisioning because each browser registration must be tenant-, session-, and expiry-bound; ARI cannot create endpoint/auth/AOR objects, while Realtime lets Orchard create and revoke those rows just in time without reloading static PBX configuration.
/// </summary>
internal interface IAsteriskPjsipRealtimeCredentialStore
{
    Task UpsertAsync(
        AsteriskPjsipRealtimeCredential credential,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string authorizationUser,
        CancellationToken cancellationToken = default);
}
