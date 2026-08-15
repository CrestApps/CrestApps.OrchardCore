namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The outcome of reconciling a checkout session's durable attempts against the payment providers'
/// authoritative APIs.
/// </summary>
public sealed class CheckoutReconciliationResult
{
    /// <summary>
    /// Whether every obligation on the session is settled by a verified, succeeded attempt.
    /// </summary>
    public bool IsFullySettled { get; set; }

    /// <summary>
    /// The obligation ids that are confirmed settled.
    /// </summary>
    public IList<string> SettledObligationIds { get; } = [];

    /// <summary>
    /// The obligation ids that are still outstanding (no verified successful attempt).
    /// </summary>
    public IList<string> OutstandingObligationIds { get; } = [];

    /// <summary>
    /// The obligation ids that were reported as failed by the provider.
    /// </summary>
    public IList<string> FailedObligationIds { get; } = [];
}
