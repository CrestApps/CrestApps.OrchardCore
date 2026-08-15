namespace CrestApps.OrchardCore.Subscriptions.Core;

/// <summary>
/// Throttles anonymous payment attempts to mitigate automated abuse (for example card testing) of
/// the anonymous Stripe payment endpoints.
/// </summary>
public interface IPaymentAttemptLimiter
{
    /// <summary>
    /// Records an attempt for the given <paramref name="scope"/> and <paramref name="discriminator"/>
    /// and reports whether it is allowed under the configured limit.
    /// </summary>
    /// <param name="scope">A logical bucket, typically the endpoint name (e.g. "setup-intent").</param>
    /// <param name="discriminator">
    /// A per-caller discriminator such as the client IP and/or session id. Attempts are counted per
    /// (scope, discriminator) pair.
    /// </param>
    /// <returns><see langword="true"/> when the attempt is permitted; otherwise <see langword="false"/>.</returns>
    Task<bool> TryAcquireAsync(string scope, string discriminator);
}
