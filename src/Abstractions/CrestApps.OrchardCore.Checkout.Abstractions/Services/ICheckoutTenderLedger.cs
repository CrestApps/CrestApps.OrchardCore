using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Holds what balances pay toward a checkout while the buyer pays the rest, takes it off when the checkout completes,
/// lets it go when it doesn't, and puts it back for a refund.
/// </summary>
public interface ICheckoutTenderLedger
{
    /// <summary>
    /// Returns how much of a balance other checkouts hold now.
    /// </summary>
    Task<decimal> GetHeldByOthersAsync(string providerKey, string reference, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Holds <see cref="TenderLine.Amount"/> of a balance for the checkout, when that much is left on it.
    /// </summary>
    Task<TenderAvailability> HoldAsync(CheckoutFlow flow, TenderLine tender, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lets go of the checkout's holds on balances that no longer pay toward it.
    /// </summary>
    Task ReleaseOthersAsync(string sessionId, IEnumerable<TenderLine> keep, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes what the checkout holds off each balance. Safe to call again.
    /// </summary>
    Task RedeemAsync(CheckoutFlow flow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lets go of everything the checkout holds.
    /// </summary>
    Task ReleaseAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns what was taken off balances for the checkout and not put back yet.
    /// </summary>
    Task<IReadOnlyList<CheckoutTenderHold>> GetRedeemedAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts up to <paramref name="amount"/> back on the balances the checkout used, the last one used first. Returns
    /// how much was put back.
    /// </summary>
    Task<decimal> ReturnAsync(string sessionId, decimal amount, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// The lock every change to a balance takes, the ledger's and a provider's own (such as a staff adjustment), so they
/// never write over each other.
/// </summary>
public static class CheckoutTenderLocks
{
    public static string GetKey(string providerKey, string reference)
        => $"TENDER_{providerKey}_{reference}";
}

/// <summary>
/// Whether a balance could be held.
/// </summary>
public enum TenderAvailability
{
    Available,

    /// <summary>
    /// Less is left on it than the checkout wanted, because it was spent or held elsewhere.
    /// </summary>
    Insufficient,

    /// <summary>
    /// It can't pay at all now: disabled, expired, or gone.
    /// </summary>
    Unavailable,

    /// <summary>
    /// Its lock couldn't be taken; trying again may work.
    /// </summary>
    Busy,
}
