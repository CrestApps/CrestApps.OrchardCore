using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Decides whether a refund can be started for a payment attempt, so the payments ledger offers the refund
/// form exactly where the form accepts it.
/// </summary>
internal static class PaymentAttemptRefundPolicy
{
    /// <summary>
    /// Gets whether a refund can be started for the attempt. Only a settled payment with a real gateway
    /// transaction can be refunded: a promise to pay later collected nothing, so it has nothing to give back.
    /// </summary>
    /// <param name="attempt">The payment attempt.</param>
    /// <param name="displayText">The display text service, which resolves the attempt's payment provider.</param>
    public static bool CanRefund(PaymentAttempt attempt, TransactionDisplayText displayText)
        => attempt is not null &&
            attempt.State == PaymentAttemptState.Succeeded &&
            !string.IsNullOrEmpty(attempt.TransactionId) &&
            displayText.FindPaymentProvider(attempt.ProviderKey)?.Capabilities?.DefersPayment != true;
}
