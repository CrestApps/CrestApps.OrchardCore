using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Throttling;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Kept for one release so code outside this repository that calls <see cref="IPaymentAttemptLimiter"/> keeps working. It
/// counts each scope and discriminator against the <c>payment-legacy</c> attempt policy (20 per 5 minutes), which is
/// changed by name under <c>CrestApps_Throttling:AttemptPolicies</c> like any other. Of <see cref="PaymentRateLimitOptions"/>
/// only a <c>PermitLimit</c> of zero or less is still read, and turns it off. New code uses <see cref="IAttemptLimiter"/>
/// with a named policy.
/// </summary>
[Obsolete("Use IAttemptLimiter with a registered attempt policy. This adapter is removed in the next release.")]
public sealed class PaymentAttemptLimiter : IPaymentAttemptLimiter
{
    /// <summary>
    /// The attempt policy the legacy scopes are counted against.
    /// </summary>
    public const string LegacyPolicyName = "payment-legacy";

    private readonly IAttemptLimiter _limiter;
    private readonly PaymentRateLimitOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentAttemptLimiter"/> class.
    /// </summary>
    public PaymentAttemptLimiter(IAttemptLimiter limiter, IOptions<PaymentRateLimitOptions> options)
    {
        _limiter = limiter;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<bool> AcquireAsync(string scope, string discriminator)
    {
        ArgumentException.ThrowIfNullOrEmpty(scope);
        ArgumentException.ThrowIfNullOrEmpty(discriminator);

        if (_options.PermitLimit <= 0)
        {
            return true;
        }

        return (await _limiter.AcquireAsync(LegacyPolicyName, scope + ":" + discriminator)).Allowed;
    }
}
