using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Builds the portable, non-null claim keys used to enforce database uniqueness for call sessions and
/// interaction events. The same computation is shared by the YesSql index providers and the upgrade
/// migrations so backfilled and freshly indexed rows resolve to identical keys.
/// </summary>
public static class ContactCenterClaimKeys
{
    /// <summary>
    /// Builds the canonical provider-call claim key.
    /// </summary>
    /// <param name="providerName">The canonical provider technical name.</param>
    /// <param name="providerCallId">The provider call identifier.</param>
    /// <param name="itemId">The item identifier used as the non-null fallback when there is no provider call identifier.</param>
    /// <returns>
    /// <c>{providerName}|{providerCallId}</c> when <paramref name="providerCallId"/> is present; otherwise
    /// <paramref name="itemId"/> so sessions without a provider call cannot collide.
    /// </returns>
    public static string BuildProviderCallClaim(string providerName, string providerCallId, string itemId)
    {
        if (string.IsNullOrEmpty(providerCallId))
        {
            return itemId;
        }

        return $"{providerName}|{providerCallId}";
    }

    /// <summary>
    /// Builds the event idempotency claim key.
    /// </summary>
    /// <param name="idempotencyKey">The provider-supplied idempotency key, when present.</param>
    /// <param name="itemId">The item identifier used as the non-null fallback when there is no idempotency key.</param>
    /// <returns>
    /// <paramref name="idempotencyKey"/> when it is present; otherwise <paramref name="itemId"/> so events
    /// without an idempotency key cannot collide.
    /// </returns>
    public static string BuildEventIdempotencyClaim(string idempotencyKey, string itemId)
    {
        return string.IsNullOrEmpty(idempotencyKey)
            ? itemId
            : idempotencyKey;
    }

    /// <summary>
    /// Builds the provider-scoped idempotency key for a normalized provider voice event.
    /// </summary>
    /// <param name="providerName">The canonical provider technical name.</param>
    /// <param name="idempotencyKey">The provider-supplied raw delivery idempotency key.</param>
    /// <returns>
    /// A bounded, collision-resistant key when both a canonical provider and a raw key are present, so
    /// identical raw delivery identifiers from different providers cannot collide or exceed the database
    /// idempotency column limit; otherwise the raw <paramref name="idempotencyKey"/> unchanged (including
    /// <see langword="null"/> or empty), preserving non-provider domain-event idempotency semantics.
    /// </returns>
    public static string BuildProviderEventIdempotencyKey(string providerName, string idempotencyKey)
    {
        if (string.IsNullOrEmpty(idempotencyKey) || string.IsNullOrWhiteSpace(providerName))
        {
            return idempotencyKey;
        }

        return BuildHashedKey("provider-event:v1:", $"{providerName}\n{idempotencyKey}");
    }

    /// <summary>
    /// Builds the bounded idempotency key for one domain event projected from a provider event.
    /// </summary>
    /// <param name="providerEventKey">The bounded provider-event idempotency key.</param>
    /// <param name="eventType">The canonical Contact Center event type.</param>
    /// <returns>
    /// A bounded, collision-resistant key for the provider event and projected event type, or the provider
    /// event key unchanged when either value is absent.
    /// </returns>
    public static string BuildProviderDomainEventIdempotencyKey(string providerEventKey, string eventType)
    {
        if (string.IsNullOrEmpty(providerEventKey) || string.IsNullOrEmpty(eventType))
        {
            return providerEventKey;
        }

        return BuildHashedKey("provider-domain-event:v1:", $"{providerEventKey}\n{eventType}");
    }

    private static string BuildHashedKey(string prefix, string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return $"{prefix}{Convert.ToHexString(hash)}";
    }
}
