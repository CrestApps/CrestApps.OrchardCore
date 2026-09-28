using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// Builds the keys that serialize and de-duplicate one provider call stream. Every consumer of the same
/// stream must derive identical keys, otherwise two consumers would serialize on different locks and
/// de-duplicate against different spaces while claiming to describe the same call.
/// </summary>
public static class VoiceIngressKeys
{
    // These prefixes are persisted in stored idempotency keys and are taken by in-flight distributed
    // locks, so they are deliberately left exactly as they were before this computation moved down to
    // the telephony layer. Changing either string would make an upgraded node de-duplicate against a
    // different key space than a previous-version node, and make both nodes serialize the same provider
    // stream on different locks during a rolling upgrade.
    private const string _ingestionLockPrefix = "ContactCenterProviderVoiceEvent:";
    private const string _eventIdempotencyPrefix = "provider-event:v1:";

    /// <summary>
    /// Builds the distributed-lock key that serializes one canonical provider call stream.
    /// </summary>
    /// <param name="providerName">The canonical provider technical name.</param>
    /// <param name="providerCallId">The provider call identifier.</param>
    /// <returns>A bounded lock key derived from the provider identity and the provider call identifier.</returns>
    public static string BuildIngestionLockKey(string providerName, string providerCallId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{providerName}\n{providerCallId}"));

        return $"{_ingestionLockPrefix}{Convert.ToHexString(bytes)}";
    }

    /// <summary>
    /// Builds the provider-scoped idempotency key for one normalized provider delivery.
    /// </summary>
    /// <param name="providerName">The canonical provider technical name.</param>
    /// <param name="idempotencyKey">The provider-supplied raw delivery idempotency key.</param>
    /// <returns>
    /// A bounded, collision-resistant key when both a canonical provider and a raw key are present, so
    /// identical raw delivery identifiers from different providers cannot collide or exceed the database
    /// idempotency column limit; otherwise the raw <paramref name="idempotencyKey"/> unchanged (including
    /// <see langword="null"/> or empty), preserving non-provider idempotency semantics.
    /// </returns>
    public static string BuildEventIdempotencyKey(string providerName, string idempotencyKey)
    {
        if (string.IsNullOrEmpty(idempotencyKey) || string.IsNullOrWhiteSpace(providerName))
        {
            return idempotencyKey;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{providerName}\n{idempotencyKey}"));

        return $"{_eventIdempotencyPrefix}{Convert.ToHexString(bytes)}";
    }
}
