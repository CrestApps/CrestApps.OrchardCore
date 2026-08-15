namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Throttles payment attempts to mitigate automated abuse (for example card testing) of the anonymous
/// payment endpoints.
/// </summary>
public interface IPaymentAttemptLimiter
{
    /// <summary>
    /// Records an attempt for the given scope and discriminator and reports whether it is allowed under
    /// the configured limit.
    /// </summary>
    /// <param name="scope">A logical bucket, typically the endpoint name (for example "payment-intent").</param>
    /// <param name="discriminator">A per-caller discriminator such as the client IP and/or session id.</param>
    Task<bool> TryAcquireAsync(string scope, string discriminator);
}
