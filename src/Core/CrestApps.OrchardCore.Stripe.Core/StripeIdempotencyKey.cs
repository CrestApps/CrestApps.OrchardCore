using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Builds deterministic Stripe idempotency keys. The same logical operation (identified by its scope
/// and salient parameters) always yields the same key, so accidental retries — network retries, double
/// submits, or duplicate webhook-driven actions — are collapsed by Stripe into a single side effect.
/// When any salient parameter changes (e.g. the customer switches payment method after a failure) the
/// key changes too, allowing a legitimate new attempt.
/// </summary>
public static class StripeIdempotencyKey
{
    /// <summary>
    /// Computes a stable idempotency key from a scope and an ordered set of parameters. The parameters
    /// are hashed so no sensitive value is exposed in the key and the result stays within Stripe's
    /// 255-character limit.
    /// </summary>
    public static string Compute(string scope, params string[] parts)
    {
        ArgumentException.ThrowIfNullOrEmpty(scope);

        var raw = string.Join('|', parts?.Select(static p => p ?? string.Empty) ?? []);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));

        return $"{scope}_{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
