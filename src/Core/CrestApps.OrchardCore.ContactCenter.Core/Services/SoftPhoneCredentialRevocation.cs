using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Centralizes browser soft-phone credential revocation for the agent sign-out lifecycle so every path that
/// ends an agent's session — an explicit or external sign-out, a security-stamp rejection, and the stale-session
/// cleanup sweep that catches pure cookie expiry — tears down the same credentials with identical, failure-isolated
/// behavior. A single revoker error is logged and swallowed so it cannot break the surrounding sign-out or cleanup.
/// </summary>
internal static class SoftPhoneCredentialRevocation
{
    /// <summary>
    /// Revokes the browser soft-phone credentials owned by the specified user across every registered revoker,
    /// isolating and logging individual revoker failures so one provider error cannot break the caller.
    /// </summary>
    /// <param name="revokers">The credential revokers to invoke.</param>
    /// <param name="userId">The authenticated user identifier whose credentials must be revoked.</param>
    /// <param name="reason">The reason recorded for the revocation.</param>
    /// <param name="logger">The logger used to record revocation failures.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static async Task RevokeForUserAsync(
        IEnumerable<ISoftPhoneCredentialRevoker> revokers,
        string userId,
        string reason,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var revoker in revokers)
        {
            try
            {
                await revoker.RevokeForUserAsync(userId, reason, cancellationToken);
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning(
                        "Soft-phone credential revocation failed for provider '{ProviderName}' and user '{UserId}'. Error type: {ErrorType}.",
                        revoker.ProviderName,
                        userId.SanitizeLogValue(),
                        ex.GetType().Name);
                }
            }
        }
    }
}
