namespace CrestApps.OrchardCore.Marketplace.Services;

/// <summary>
/// What a payout provider answered for one payout: the transfer it made, a refusal, or that nothing can be said yet.
/// </summary>
public sealed record VendorPayoutResult
{
    /// <summary>
    /// Gets the provider's reference for the transfer, when one was made.
    /// </summary>
    public string TransferId { get; init; }

    /// <summary>
    /// Gets why no transfer was made, or why the provider can't tell yet.
    /// </summary>
    public string Failure { get; init; }

    /// <summary>
    /// Gets a value indicating whether the payout must stay pending and be asked about again with the same idempotency
    /// key: the provider couldn't be reached, answered with a temporary error, or said the key was used for another
    /// request. Only a refusal is proof that no transfer exists.
    /// </summary>
    public bool RetryLater { get; init; }

    /// <summary>
    /// The transfer was made.
    /// </summary>
    /// <param name="transferId">The provider's reference for the transfer.</param>
    public static VendorPayoutResult Paid(string transferId)
        => new() { TransferId = transferId };

    /// <summary>
    /// The provider refused the transfer, so none exists and the next try may use a new key.
    /// </summary>
    /// <param name="failure">Why.</param>
    public static VendorPayoutResult Refused(string failure)
        => new() { Failure = failure };

    /// <summary>
    /// Whether a transfer exists is not known, so the payout stays pending with its key.
    /// </summary>
    /// <param name="failure">Why.</param>
    public static VendorPayoutResult Unsettled(string failure)
        => new() { Failure = failure, RetryLater = true };
}
