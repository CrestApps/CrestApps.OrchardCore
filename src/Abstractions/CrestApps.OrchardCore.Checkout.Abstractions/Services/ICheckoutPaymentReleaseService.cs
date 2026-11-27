namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Sets aside the payments begun for a checkout before it is edited. A payment is begun for the total of the moment; a
/// buyer who then changes shipping, a code or a balance must pay the new total, so the old payment is canceled at the
/// provider and superseded, which kills its client secret.
/// </summary>
public interface ICheckoutPaymentReleaseService
{
    /// <summary>
    /// Cancels and supersedes every live payment of the checkout, under the checkout's lock.
    /// </summary>
    /// <param name="sessionId">The checkout session.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the edit may go ahead.</returns>
    Task<CheckoutPaymentReleaseResult> ReleaseAsync(string sessionId, CancellationToken cancellationToken = default);
}
