using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Implemented by a telephony provider that can enrich stored user tokens with provider-account metadata,
/// such as the remote user id, email address, or assigned phone number.
/// </summary>
public interface ITelephonyUserConnectionMetadataProvider
{
    /// <summary>
    /// Enriches the given user tokens with provider-account metadata for the connected remote user.
    /// </summary>
    /// <param name="tokens">The current user tokens.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The enriched tokens, or the original tokens when no additional metadata could be resolved.
    /// </returns>
    Task<TelephonyUserTokens> EnrichTokensAsync(
        TelephonyUserTokens tokens,
        CancellationToken cancellationToken = default);
}
