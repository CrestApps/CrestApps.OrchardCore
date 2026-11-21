using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Setting aside the payment attempts a checkout no longer needs: one begun for another total, one begun for an
/// obligation the invoice no longer has, and one begun with another payment method.
/// </summary>
public sealed partial class DefaultCheckoutEngine
{
    private const string SupersededReason = "The total changed before it was paid.";

    // The attempts of one obligation and provider that are over: declined, canceled or set aside.
    private static int CountEndedAttempts(IEnumerable<PaymentAttempt> attempts, string obligationId, string providerKey)
        => attempts.Count(candidate =>
            string.Equals(candidate.ObligationId, obligationId, StringComparison.Ordinal) &&
            string.Equals(candidate.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase) &&
            (candidate.State is PaymentAttemptState.Failed or PaymentAttemptState.Canceled || candidate.IsSuperseded));

    private static bool MatchesInvoice(PaymentAttempt attempt, CheckoutInvoice invoice)
    {
        var (amount, taxAmount) = GetObligationAmounts(invoice, attempt.ObligationId);

        return attempt.ExpectedAmount == amount && attempt.ExpectedTaxAmount == taxAmount;
    }

    // An attempt begun for another total is set aside only once its provider says it took nothing and has canceled it, as
    // an edit's release does: one set aside here alone kept its client secret live at the provider, and the declines
    // against it named an attempt nothing counted. A payment the buyer finished in another tab is never dropped.
    private async Task<bool> TrySupersedeAsync(CheckoutSession session, PaymentAttempt attempt, CancellationToken cancellationToken)
    {
        if (_providerResolver.GetProvider(attempt.ProviderKey) is { } provider)
        {
            try
            {
                // A provider that defers payment says "succeeded" as soon as it is begun, though no money moved.
                if (attempt.State == PaymentAttemptState.Pending && provider.Capabilities?.DefersPayment != true &&
                    (await provider.VerifyAsync(new VerifyPaymentContext { Session = session, Attempt = attempt }, cancellationToken)).Status == PaymentStatus.Succeeded)
                {
                    _logger.LogWarning("Attempt '{AttemptId}' of checkout '{SessionId}' was paid before the total changed, so it stands.", attempt.ItemId, session.SessionId);

                    return false;
                }

                var canceled = await provider.CancelAsync(new CancelPaymentContext { Session = session, Attempt = attempt, Reason = SupersededReason }, cancellationToken);

                if (!canceled.Succeeded)
                {
                    _logger.LogWarning("Attempt '{AttemptId}' of checkout '{SessionId}' could not be canceled before setting it aside, so it stands: {Error}", attempt.ItemId, session.SessionId, canceled.ErrorMessage);

                    return false;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Could not check attempt '{AttemptId}' of checkout '{SessionId}' before setting it aside, so it stands.", attempt.ItemId, session.SessionId);

                return false;
            }
        }

        await SupersedeAsync(attempt, cancellationToken);

        return true;
    }

    // Sets aside the live attempts other providers made for this obligation. Returns false, leaving everything as it
    // is, when one of them was paid, can't be checked, or its provider won't confirm it's canceled.
    private async Task<bool> TryReleaseOtherProvidersAsync(
        CheckoutSession session,
        string providerKey,
        string obligationId,
        List<PaymentAttempt> existingAttempts,
        CancellationToken cancellationToken)
    {
        var others = existingAttempts
            .Where(candidate =>
                string.Equals(candidate.ObligationId, obligationId, StringComparison.Ordinal) &&
                !string.Equals(candidate.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase) &&
                !candidate.IsSuperseded &&
                candidate.State is PaymentAttemptState.Created or PaymentAttemptState.Pending or PaymentAttemptState.Succeeded)
            .ToList();

        foreach (var other in others)
        {
            if (other.State == PaymentAttemptState.Succeeded || _providerResolver.GetProvider(other.ProviderKey) is not { } otherProvider)
            {
                return false;
            }

            // An attempt still Created, with no reference, is one whose begin may have ended without the provider's
            // answer (a server error, a timeout): the provider may have charged it anyway. Until the provider can be
            // expected to find it, nothing is set aside.
            if (other.State == PaymentAttemptState.Created &&
                string.IsNullOrEmpty(other.ProviderReference) &&
                other.CreatedUtc != default &&
                _clock.UtcNow - other.CreatedUtc < _unansweredBeginWindow)
            {
                return false;
            }

            try
            {
                if (other.State is PaymentAttemptState.Pending or PaymentAttemptState.Created)
                {
                    var verification = await otherProvider.VerifyAsync(new VerifyPaymentContext { Session = session, Attempt = other }, cancellationToken);

                    if (verification.Status == PaymentStatus.Succeeded)
                    {
                        _logger.LogWarning("Attempt '{AttemptId}' of checkout '{SessionId}' was paid through '{ProviderKey}', so no payment is begun with '{NewProviderKey}'.", other.ItemId, session.SessionId, other.ProviderKey, providerKey);

                        return false;
                    }
                }

                var canceled = await otherProvider.CancelAsync(
                    new CancelPaymentContext
                    {
                        Session = session,
                        Attempt = other,
                        Reason = "The buyer paid with another payment method.",
                    },
                    cancellationToken);

                if (!canceled.Succeeded)
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not set aside attempt '{AttemptId}' of checkout '{SessionId}', so no payment is begun with '{ProviderKey}'.", other.ItemId, session.SessionId, providerKey);

                return false;
            }

            other.State = PaymentAttemptState.Canceled;
            other.IsSuperseded = true;
            other.FailureReason = "The buyer paid with another payment method.";

            await _attemptStore.UpdateAsync(other, cancellationToken);
            existingAttempts.Remove(other);
        }

        return true;
    }

    private async Task SupersedeAsync(PaymentAttempt attempt, CancellationToken cancellationToken)
    {
        attempt.State = PaymentAttemptState.Canceled;
        attempt.IsSuperseded = true;
        attempt.FailureReason = SupersededReason;

        await _attemptStore.UpdateAsync(attempt, cancellationToken);
    }

    // Attempts begun for obligations the invoice no longer has, such as a card payment begun before a gift card was
    // applied that now pays everything, are set aside, so they neither keep the checkout waiting nor get charged.
    private async Task SupersedeUnexpectedAttemptsAsync(CheckoutSession session, IReadOnlyCollection<string> expectedObligations, CancellationToken cancellationToken)
    {
        foreach (var attempt in await _attemptStore.GetBySessionAsync(session.SessionId, cancellationToken))
        {
            if (attempt.State is PaymentAttemptState.Created or PaymentAttemptState.Pending &&
                !attempt.IsSuperseded &&
                !expectedObligations.Contains(attempt.ObligationId))
            {
                await TrySupersedeAsync(session, attempt, cancellationToken);
            }
        }
    }
}
