using System.Globalization;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Marketplace.Models;

namespace CrestApps.OrchardCore.Marketplace.Services;

/// <summary>
/// Payouts whose transfer the provider hasn't shown: they stay pending under their key, and staff confirm by hand, after
/// <see cref="UnconfirmedPayoutReviewAfter"/>, that no transfer was made.
/// </summary>
public sealed partial class MarketplaceService
{
    /// <summary>
    /// How long a pending payout whose transfer the provider doesn't have stays pending before staff can confirm that no
    /// transfer was made. It is longer than Stripe keeps an idempotency key (24 hours) and than its incident recoveries.
    /// </summary>
    public static readonly TimeSpan UnconfirmedPayoutReviewAfter = TimeSpan.FromDays(3);

    // Keeps on the payout since when its transfer hasn't been found, so staff can settle it by hand once the review time
    // has passed. Returns what to tell them.
    private async Task<string> RecordNotFoundAsync(VendorPayout payout, string disbursementId, string failure, CancellationToken cancellationToken)
    {
        var stored = await _payouts.FindByIdAsync(payout.ItemId, cancellationToken) ?? payout;

        stored.UnconfirmedSinceUtc ??= _clock.UtcNow;
        stored.DisbursementId = disbursementId ?? stored.DisbursementId;
        stored.Failure = failure;

        await _payouts.UpdateAsync(stored, cancellationToken);

        if (!IsAwaitingReview(stored, _clock.UtcNow))
        {
            return failure;
        }

        var since = await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(stored.UnconfirmedSinceUtc.Value, DateTimeKind.Utc));

        return S["No transfer for the payout of order {0} has been found since {1}. Check the vendor's account at the payout provider: if no transfer was made, choose \"No transfer was made\" for it on the vendor's page, and the next payout pays it again.", payout.OrderNumber, since.ToString("g", CultureInfo.CurrentCulture)].Value;
    }

    /// <summary>
    /// Returns whether a payout is pending and its provider hasn't shown its transfer for
    /// <see cref="UnconfirmedPayoutReviewAfter"/>, so staff may confirm with <see cref="ConfirmNoTransferAsync"/> that none
    /// was made.
    /// </summary>
    /// <param name="payout">The payout.</param>
    /// <param name="utcNow">The current time.</param>
    public static bool IsAwaitingReview(VendorPayout payout, DateTime utcNow)
        => payout is { Status: VendorPayoutStatus.Pending, UnconfirmedSinceUtc: { } since } && utcNow - since >= UnconfirmedPayoutReviewAfter;

    /// <summary>
    /// Records that staff checked the vendor's account at the payout provider and no transfer was made for a payout
    /// awaiting review (<see cref="IsAwaitingReview"/>). The provider is asked once more; when it still has none, the
    /// payout's disbursement is closed and the payout failed, so the next payout run pays its earnings under a new key.
    /// Returns why it wasn't, or <see langword="null"/>.
    /// </summary>
    /// <param name="vendor">The vendor.</param>
    /// <param name="payoutId">The payout.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    public async Task<string> ConfirmNoTransferAsync(Vendor vendor, string payoutId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vendor);

        if (!_payoutServices.CanDisburse)
        {
            return S["Turn on Disbursements: vendor payouts are sent as disbursements."].Value;
        }

        var (locker, locked) = await _payoutServices.DistributedLock.TryAcquireLockAsync($"Marketplace:Payout:{vendor.ItemId}", TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(10));

        if (!locked)
        {
            return S["A payout of this vendor is already running. Try again in a moment."].Value;
        }

        try
        {
            return await ConfirmNoTransferLockedAsync(vendor, payoutId, cancellationToken);
        }
        finally
        {
            if (locker is not null)
            {
                await locker.DisposeAsync();
            }
        }
    }

    private async Task<string> ConfirmNoTransferLockedAsync(Vendor vendor, string payoutId, CancellationToken cancellationToken)
    {
        var payout = await _payouts.FindByIdAsync(payoutId, cancellationToken);

        if (payout?.VendorId != vendor.ItemId || !IsAwaitingReview(payout, _clock.UtcNow))
        {
            return S["Only a pending payout whose transfer hasn't been found for {0} days can be marked as not made.", UnconfirmedPayoutReviewAfter.TotalDays].Value;
        }

        if (_payoutProviders.FirstOrDefault(candidate => string.Equals(candidate.Name, payout.Provider, StringComparison.OrdinalIgnoreCase)) is not { } provider)
        {
            return S["The payout of order {0} was started with {1}, which isn't enabled now.", payout.OrderNumber, payout.Provider].Value;
        }

        var disbursements = _payoutServices.Disbursements;
        var disbursement = await _payoutServices.DisbursementManager.FindByIdempotencyKeyAsync(payout.IdempotencyKey, cancellationToken);

        if (disbursement is not { Status: DisbursementStatus.Submitted or DisbursementStatus.Settled })
        {
            return S["The payout of order {0} isn't waiting on its transfer. Pay out the vendor to finish it.", payout.OrderNumber].Value;
        }

        if (await provider.FindTransferAsync(disbursement, cancellationToken) is { } found)
        {
            return found.RetryLater
                ? S["The payout provider couldn't be asked about the payout of order {0}. Try again later.", payout.OrderNumber].Value
                : S["The payout provider has a transfer for the payout of order {0} now. Pay out the vendor to record it.", payout.OrderNumber].Value;
        }

        var reason = S["Staff confirmed that no transfer was made for the payout of order {0}.", payout.OrderNumber].Value;
        var closed = disbursement.Status == DisbursementStatus.Settled ? DisbursementStatus.Returned : DisbursementStatus.Failed;

        if (!await disbursements.MarkClosedAsync(disbursement, closed, null, reason, cancellationToken))
        {
            return S["The disbursement of the payout of order {0} couldn't be closed.", payout.OrderNumber].Value;
        }

        // The closed disbursement is the refusal: recorded as one, the earnings get a new key for the next run.
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

        await SendAsync(disbursements, vendor, provider, payout, today, resend: true, [], cancellationToken);
        await CommitAsync(cancellationToken);

        return null;
    }
}
