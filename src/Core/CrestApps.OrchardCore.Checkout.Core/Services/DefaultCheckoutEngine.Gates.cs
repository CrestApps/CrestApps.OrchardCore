using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Transactions.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The checks a payment goes through before any gate holds what it pays for, and the one that keeps a checkout still being
/// paid from expiring.
/// </summary>
public sealed partial class DefaultCheckoutEngine
{
    // A checkout waiting on its provider keeps the time it started waiting. The sweep asks the provider again on every run,
    // and bumping the time each run would keep an abandoned 3-D Secure checkout fresh for good, so it would never expire and
    // release the stock, coupon and tender it holds.
    private void MarkPaymentPending(CheckoutSession session)
    {
        if (session.Status == CheckoutSessionStatus.PaymentPending)
        {
            return;
        }

        session.Status = CheckoutSessionStatus.PaymentPending;
        session.ModifiedUtc = _clock.UtcNow;
    }

    // The network the payment comes from is kept on the checkout, so the card-testing protection counts a decline reported
    // later, by a webhook or the sweep, against it too.
    private async Task KeepClientNetworkAsync(CheckoutSession session, string clientNetworkKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(clientNetworkKey) ||
            (session.TryGet<CheckoutClientInfo>(out var client) && string.Equals(client.NetworkKey, clientNetworkKey, StringComparison.Ordinal)))
        {
            return;
        }

        session.Put(new CheckoutClientInfo { NetworkKey = clientNetworkKey });
        session.ModifiedUtc = _clock.UtcNow;

        await _sessionStore.SaveAsync(session, cancellationToken);
    }

    // A provider that can't take this payment is refused before the gates run, so nothing is held for a payment that was
    // never going to be made, such as a pay link's transaction held by a Bank Transfer that may not settle a balance. A gate
    // may lower what is owed, never add an obligation, so the check after the gates still runs on what is left.
    private static PaymentBeginOutcome RefuseUnsupportedBeforeGates(TelemetryScope telemetry, ICheckoutPaymentProvider provider, CheckoutSession session, CheckoutInvoice invoice)
    {
        var obligations = CheckoutObligations.GetExpectedObligationIds(invoice);

        return obligations.Count > 0 &&
            !TryValidateCapabilities(provider, obligations, session.SettlesExistingBalance, session.SavePaymentMethod, out var error)
            ? Refused(telemetry, provider, "unsupported", PaymentBeginOutcome.Failure(error))
            : null;
    }

    // A checkout waiting on its payment is expired only once the provider canceled every payment still open: one the
    // provider is still processing, such as a bank debit, must be left to finish. What was canceled is saved either way.
    private async Task<bool> KeepWhileStillPayingAsync(CheckoutSession session, CancellationToken cancellationToken)
    {
        if (session.Status != CheckoutSessionStatus.PaymentPending ||
            !(await _attemptStore.GetBySessionAsync(session.SessionId, cancellationToken)).Any(attempt => attempt.State is PaymentAttemptState.Created or PaymentAttemptState.Pending))
        {
            return false;
        }

        await _session.SaveChangesAsync(cancellationToken);

        return true;
    }
}
